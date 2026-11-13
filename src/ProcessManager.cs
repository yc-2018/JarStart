using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace JarStart
{
    /// <summary>某个 jar 当前的运行态</summary>
    public class JarRuntime
    {
        public JarStatus Status = JarStatus.Stopped;
        public int Pid;
        public int ExitCode = -1;
        public DateTime StartedAt = DateTime.MinValue;
        public string Error = "";
        public string JavaPath = "";
        public bool StopRequested;
        public int Port;                    // 从启动日志里识别到的端口，0 表示还没识别出来
        public Process Proc;
    }

    /// <summary>
    /// 负责启动/停止 jar 进程，并把子进程输出转成日志行。
    /// 所有事件都在后台线程触发，界面层需要自己切回 UI 线程。
    /// </summary>
    public class ProcessManager
    {
        // Job 必须一直活着，不能被 GC 回收（回收时句柄被关掉会误杀子进程）
        private static JobGuard _job;

        private readonly object _sync = new object();
        private readonly Dictionary<string, JarRuntime> _map = new Dictionary<string, JarRuntime>();
        private readonly LogStore _log;

        /// <summary>jarId 状态发生变化</summary>
        public event Action<string> StatusChanged;

        /// <summary>
        /// 勾上「退出时停止所有子进程」时为 true：新启动的进程会登记到 Job Object，
        /// 启动器一死它们就被 Windows 一起带走。
        /// </summary>
        public bool KillChildrenOnExit = true;

        public ProcessManager(LogStore log)
        {
            _log = log;
        }

        /// <summary>退出时收尾：关闭 Job，顺带把还活着的子进程清掉</summary>
        public static void ShutdownJob()
        {
            if (_job != null) _job.Close();
        }

        public JarRuntime Runtime(string jarId)
        {
            lock (_sync)
            {
                JarRuntime rt;
                if (!_map.TryGetValue(jarId, out rt)) return new JarRuntime();
                return rt;
            }
        }

        public JarStatus StatusOf(string jarId)
        {
            return Runtime(jarId).Status;
        }

        public bool IsRunning(string jarId)
        {
            var s = StatusOf(jarId);
            return s == JarStatus.Running || s == JarStatus.Starting;
        }

        /// <summary>启动一个 jar；返回 false 时 error 里是中文失败原因</summary>
        public bool Start(JarItem item, string globalJava, out string error)
        {
            error = "";
            if (item == null) { error = "没有选择 jar"; return false; }
            if (IsRunning(item.Id))
            {
                error = "该 jar 已在运行中";
                return false;
            }
            if (!item.JarExists)
            {
                error = "找不到 jar 文件：" + item.JarPath;
                return false;
            }

            var javaPath = JdkResolver.ResolveJava(item.JavaPath, globalJava);
            var rt = new JarRuntime();
            rt.JavaPath = javaPath;

            var args = BuildArgs(item);
            var psi = new ProcessStartInfo(javaPath, args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = ResolveWorkDir(item);

            ApplyEncoding(psi, item.Encoding);
            ApplyEnv(psi, item.EnvVars);

            _log.AppendSys(item.Id, "启动命令：" + javaPath + " " + args);
            if (!string.IsNullOrWhiteSpace(globalJava) && !string.IsNullOrWhiteSpace(item.JavaPath))
                _log.AppendSys(item.Id, "Java 来源：本项指定");
            else if (!string.IsNullOrWhiteSpace(globalJava))
                _log.AppendSys(item.Id, "Java 来源：全局默认");
            else
                _log.AppendSys(item.Id, "Java 来源：系统环境变量");

            Process proc;
            try
            {
                proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                error = "启动失败：" + ex.Message;
                rt.Status = JarStatus.Failed;
                rt.Error = error;
                Put(item.Id, rt);
                _log.AppendSys(item.Id, error);
                Raise(item.Id);
                return false;
            }

            if (proc == null)
            {
                error = "启动失败：无法创建进程";
                rt.Status = JarStatus.Failed;
                rt.Error = error;
                Put(item.Id, rt);
                _log.AppendSys(item.Id, error);
                Raise(item.Id);
                return false;
            }

            rt.Proc = proc;
            rt.Pid = proc.Id;
            rt.StartedAt = DateTime.Now;
            rt.Status = JarStatus.Running;
            rt.ExitCode = -1;
            rt.Error = "";
            rt.StopRequested = false;
            rt.Port = 0;
            Put(item.Id, rt);

            // 登记到 Job Object：启动器被强杀/崩溃时，子进程会被系统一起结束
            if (KillChildrenOnExit)
            {
                try
                {
                    if (_job == null) _job = new JobGuard();
                    if (_job.Create()) _job.Assign(proc.Handle);
                }
                catch { }
            }

            string jarId = item.Id;
            proc.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                _log.Append(jarId, LogLevel.Out, e.Data);
                NotePort(jarId, e.Data);
            };
            proc.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                _log.Append(jarId, LogLevel.Err, e.Data);
                NotePort(jarId, e.Data);
            };
            proc.Exited += delegate { OnExited(jarId); };
            proc.EnableRaisingEvents = true;

            try { proc.BeginOutputReadLine(); } catch { }
            try { proc.BeginErrorReadLine(); } catch { }

            _log.AppendSys(item.Id, "进程已启动，PID = " + proc.Id);
            Raise(item.Id);
            return true;
        }

        /// <summary>停止一个 jar；force=true 时直接强杀整棵进程树</summary>
        public void Stop(string jarId, bool force)
        {
            JarRuntime rt;
            lock (_sync)
            {
                if (!_map.TryGetValue(jarId, out rt)) return;
            }
            var proc = rt.Proc;
            if (proc == null) return;

            try
            {
                if (proc.HasExited) return;
            }
            catch { return; }

            rt.StopRequested = true;
            _log.AppendSys(jarId, force ? "正在强制结束进程树…" : "正在停止进程…");

            try
            {
                KillTree(proc.Id, force);
                if (!force)
                {
                    try { proc.WaitForExit(2000); } catch { }
                    bool alive;
                    try { alive = !proc.HasExited; } catch { alive = false; }
                    if (alive)
                    {
                        KillTree(proc.Id, true);
                        try { proc.WaitForExit(2000); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.AppendSys(jarId, "停止时出错：" + ex.Message);
            }
        }

        public void StopAll(bool force)
        {
            List<string> ids;
            lock (_sync)
            {
                ids = new List<string>(_map.Keys);
            }
            foreach (var id in ids) Stop(id, force);
        }

        public bool AnyRunning()
        {
            lock (_sync)
            {
                foreach (var kv in _map)
                {
                    if (kv.Value.Status == JarStatus.Running || kv.Value.Status == JarStatus.Starting)
                        return true;
                }
            }
            return false;
        }

        /// <summary>识别子进程输出里的端口，认出来就记到运行态里并刷新界面</summary>
        private void NotePort(string jarId, string line)
        {
            int port = PortDetector.Detect(line);
            if (port <= 0) return;

            JarRuntime rt = Runtime(jarId);
            if (rt.Port == port) return;
            rt.Port = port;
            _log.AppendSys(jarId, "检测到端口：" + port);
            Raise(jarId);
        }

        private void OnExited(string jarId)
        {
            JarRuntime rt = Runtime(jarId);
            if (rt.Proc != null)
            {
                try { rt.Proc.WaitForExit(3000); } catch { }
                try { rt.ExitCode = rt.Proc.ExitCode; } catch { }
            }
            if (rt.StopRequested)
            {
                rt.Status = JarStatus.Stopped;
                rt.ExitCode = -1;
                _log.AppendSys(jarId, "进程已停止");
            }
            else
            {
                rt.Status = JarStatus.Exited;
                _log.AppendSys(jarId, "进程已退出" +
                    (rt.ExitCode >= 0 ? "，退出码 " + rt.ExitCode : ""));
            }
            Raise(jarId);
        }

        private void Put(string jarId, JarRuntime rt)
        {
            lock (_sync)
            {
                _map[jarId] = rt;
            }
        }

        private void Raise(string jarId)
        {
            var h = StatusChanged;
            if (h != null) h(jarId);
        }

        /// <summary>拼出 java 命令行参数</summary>
        public static string BuildArgs(JarItem item)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(item.JvmArgs))
            {
                sb.Append(item.JvmArgs.Trim());
                sb.Append(' ');
            }
            sb.Append("-jar ");
            sb.Append('"');
            sb.Append(item.JarPath);
            sb.Append('"');
            if (!string.IsNullOrWhiteSpace(item.ProgramArgs))
            {
                sb.Append(' ');
                sb.Append(item.ProgramArgs.Trim());
            }
            return sb.ToString();
        }

        private static string ResolveWorkDir(JarItem item)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(item.WorkDir))
                {
                    if (Directory.Exists(item.WorkDir)) return item.WorkDir;
                }
                var dir = Path.GetDirectoryName(item.JarPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) return dir;
            }
            catch { }
            return Environment.CurrentDirectory;
        }

        private static void ApplyEncoding(ProcessStartInfo psi, string encoding)
        {
            try
            {
                var name = string.IsNullOrWhiteSpace(encoding) ? "UTF-8" : encoding.Trim();
                var enc = Encoding.GetEncoding(name);
                psi.StandardOutputEncoding = enc;
                psi.StandardErrorEncoding = enc;
            }
            catch
            {
                // 编码不支持时用系统默认，不影响启动
            }
        }

        private static void ApplyEnv(ProcessStartInfo psi, string envVars)
        {
            if (string.IsNullOrWhiteSpace(envVars)) return;
            var lines = envVars.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line.Substring(0, idx).Trim();
                var val = line.Substring(idx + 1).Trim();
                if (key.Length == 0) continue;
                try { psi.EnvironmentVariables[key] = val; } catch { }
            }
        }

        /// <summary>用 taskkill 结束整棵进程树</summary>
        private static void KillTree(int pid, bool force)
        {
            try
            {
                var args = "/PID " + pid + " /T" + (force ? " /F" : "");
                var psi = new ProcessStartInfo("taskkill.exe", args);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (var p = Process.Start(psi))
                {
                    if (p != null) p.WaitForExit(5000);
                }
            }
            catch { }
        }
    }
}
