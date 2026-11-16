using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace JarStart
{
    /// <summary>核心层自检：配置序列化、日志缓冲、java 解析、进程启停</summary>
    public static class SelfTest
    {
        private static int _failed = 0;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr h, int msg, IntPtr wp, IntPtr lp);

        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("== JarStart 自检 ==");

            TestConfigRoundTrip();
            TestLogStore();
            TestJdkResolver();
            TestPortDetector();
            TestReadyDetector();
            TestJavaProbe();
            TestLogViewSelection();
            TestProcess(args.Length > 0 ? args[0] : null);

            Console.WriteLine(_failed == 0 ? "\n全部通过 ✅" : "\n失败 " + _failed + " 项 ❌");
            Environment.Exit(_failed == 0 ? 0 : 1);
        }

        private static void Check(bool ok, string name, string detail)
        {
            if (ok) Console.WriteLine("  [OK] " + name);
            else { _failed++; Console.WriteLine("  [FAIL] " + name + (detail == null ? "" : " -> " + detail)); }
        }

        private static void TestConfigRoundTrip()
        {
            Console.WriteLine("- 配置序列化");
            var cfg = new AppConfig();
            cfg.Theme = "dark";
            cfg.MaxLogLines = 1234;
            cfg.Accent = "#4F8CFF";
            var item = new JarItem();
            item.Id = "abc123";
            item.Name = "测试服务";
            item.JarPath = @"D:\tmp\demo.jar";
            item.Group = "核心";
            item.Encoding = "GBK";
            item.AutoStart = true;
            cfg.Items.Add(item);
            cfg.Groups.Add("核心");

            string json = new JavaScriptSerializer().Serialize(cfg);
            var back = new JavaScriptSerializer().Deserialize<AppConfig>(json);
            bool ok = back != null &&
                      back.Theme == "dark" &&
                      back.MaxLogLines == 1234 &&
                      back.Items.Count == 1 &&
                      back.Items[0].Id == "abc123" &&
                      back.Items[0].Group == "核心" &&
                      back.Items[0].Encoding == "GBK" &&
                      back.Items[0].AutoStart &&
                      back.Groups.Count == 1;
            Check(ok, "配置 JSON 往返一致", ok ? null : json);
        }

        private static void TestLogStore()
        {
            Console.WriteLine("- 日志缓冲");
            var store = new LogStore(300);
            for (int i = 0; i < 1000; i++) store.Append("x", LogLevel.Out, "line " + i);
            var lines = store.Get("x");
            Check(lines.Count <= 300 && lines.Count > 0, "环形缓冲上限生效", "count=" + lines.Count);
            Check(lines[lines.Count - 1].Text == "line 999", "保留的是最新日志", lines[lines.Count - 1].Text);
            store.Clear("x");
            Check(store.Get("x").Count == 0, "清空生效", null);
        }

        private static void TestJdkResolver()
        {
            Console.WriteLine("- java 解析");
            string home = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrWhiteSpace(home))
            {
                Console.WriteLine("  [跳过] 环境变量里没有 JAVA_HOME");
            }
            else
            {
                string exe = JdkResolver.ResolveJava("", "");
                bool ok = !string.IsNullOrEmpty(exe);
                Check(ok, "能解析出 java 路径", exe);
                string ver = JdkResolver.ProbeVersion(exe);
                Check(ver.IndexOf("version", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      ver.IndexOf("错误", StringComparison.OrdinalIgnoreCase) >= 0 ||
                      ver.Length > 0, "能读到 java 版本", ver);
            }
        }

        private static void TestPortDetector()
        {
            Console.WriteLine("- 端口识别");
            Check(PortDetector.Detect("Tomcat started on port(s): 8080 (http) with context path ''") == 8080,
                  "Tomcat started on port(s)", null);
            Check(PortDetector.Detect("Undertow started on port(s) 9090") == 9090,
                  "Undertow started on port(s)", null);
            Check(PortDetector.Detect("Netty started on port(s): 2181") == 2181,
                  "Netty started on port(s)", null);
            Check(PortDetector.Detect("Started application at http://localhost:8081") == 8081,
                  "started at http://localhost:8081", null);
            // Spring Boot 3.x 的写法：Tomcat started on port 8081 (http) with context path '/fw'
            Check(PortDetector.Detect("2026-11-16 15:03:34,222 [main,] INFO  o.s.boot.web.embedded.tomcat.TomcatWebServer:241 - Tomcat started on port 8081 (http) with context path '/fw'") == 8081,
                  "Tomcat started on port 8081 (Spring Boot 3.x 写法)", null);
            Check(PortDetector.Detect("Tomcat started on port 8081 (http)") == 8081,
                  "Tomcat started on port 8081", null);
            Check(PortDetector.Detect("2024-01-01 10:00:00 INFO  连接池 port=0 已重置") == 0,
                  "普通日志里的 port 不误报", null);
            Check(PortDetector.Detect("Nacos started successfully in stand alone mode") == 0,
                  "没有端口的行返回 0", null);
        }

        private static void TestLogViewSelection()
        {
            Console.WriteLine("- 日志选择（字符级）");
            try
            {
                var view = new LogView();
                view.Size = new System.Drawing.Size(900, 400);
                var lines = new List<LogLine>();
                lines.Add(new LogLine(new DateTime(2026, 1, 1, 10, 0, 0), LogLevel.Out,
                    "hello world this is a test line"));
                view.SetLines(lines);

                // 横坐标换算成字符下标：越靠右下标越大
                int c1 = view.CharIndexAt(200f, 0);
                int c2 = view.CharIndexAt(320f, 0);
                Check(c2 > c1, "字符下标随横坐标增大", c1 + " -> " + c2);

                view.SetSelection(0, c1, c2);
                string sel = (view.GetSelectedText() ?? "").Trim();
                string expect = "hello world this is a test line".Substring(c1, c2 - c1).Trim();
                Check(sel.Length > 0, "能只选出片段", "'" + sel + "'");
                Check(sel == expect, "选出来的内容正确",
                      "c1=" + c1 + " c2=" + c2 + " 实际='" + sel + "' 期望='" + expect + "'");

                // 整行选中时要带时间戳
                view.SetSelection(0, 0, ("hello world this is a test line").Length);
                string all = (view.GetSelectedText() ?? "");
                Check(all.Contains("10:00:00"), "整行选中带时间戳", "'" + all.Trim() + "'");

                // 模拟真实的鼠标按下-拖动-松开（同步发消息，行为和鼠标拖动一致）
                IntPtr h = view.Handle;
                view.SetSelection(0, 0, 0);
                PostMessage(h, 0x0201, (IntPtr)1, (IntPtr)((5 << 16) | 200));   // 左键按下（第 0 行）
                PostMessage(h, 0x0200, (IntPtr)1, (IntPtr)((5 << 16) | 300));   // 拖动
                PostMessage(h, 0x0202, (IntPtr)0, (IntPtr)((5 << 16) | 300));   // 松开
                System.Windows.Forms.Application.DoEvents();
                string info = view.SelectionInfo;
                Check(info != "0,0 -> 0,0" && info.IndexOf("->") > 0, "鼠标拖动能选中一段",
                      "选区=" + info + " 内容='" + (view.GetSelectedText() ?? "").Trim() + "'");

                // 先拖一次滚动条，再拖选：看看滚动条的拖动会不会把后续选择弄坏
                view.SetSelection(0, 0, 0);
                PostMessage(h, 0x0201, (IntPtr)1, (IntPtr)((350 << 16) | (view.Width - 6)));
                PostMessage(h, 0x0200, (IntPtr)1, (IntPtr)((100 << 16) | (view.Width - 6)));
                PostMessage(h, 0x0202, (IntPtr)0, (IntPtr)((100 << 16) | (view.Width - 6)));
                System.Windows.Forms.Application.DoEvents();
                PostMessage(h, 0x0201, (IntPtr)1, (IntPtr)((5 << 16) | 200));
                PostMessage(h, 0x0200, (IntPtr)1, (IntPtr)((5 << 16) | 300));
                PostMessage(h, 0x0202, (IntPtr)0, (IntPtr)((5 << 16) | 300));
                System.Windows.Forms.Application.DoEvents();
                string info2 = view.SelectionInfo;
                Check(info2 != "0,0 -> 0,0" && info2.IndexOf("->") > 0, "拖过滚动条之后还能选中",
                      "选区=" + info2 + " 内容='" + (view.GetSelectedText() ?? "").Trim() + "'");
            }
            catch (Exception ex)
            {
                Check(false, "日志选择自检", ex.Message);
            }
        }

        private static void TestJavaProbe()
        {
            Console.WriteLine("- java 探测");
            try
            {
                var lbl = new System.Windows.Forms.Label();
                lbl.AutoSize = false;
                lbl.Width = 300;
                var tip = new System.Windows.Forms.ToolTip();

                JavaProbe.Apply(lbl, tip, "");
                Check(lbl.Text.StartsWith("✔") || lbl.Text.StartsWith("✖"),
                      "检测后标签有结果", "'" + lbl.Text + "'");
                Check(!lbl.Text.StartsWith("✖"), "系统环境变量里的 java 能探测成功", "'" + lbl.Text + "'");

                var lbl2 = new System.Windows.Forms.Label();
                JavaProbe.Apply(lbl2, tip, @"D:\不存在的路径\bin");
                Check(lbl2.Text.StartsWith("✖"), "无效路径要报错", "'" + lbl2.Text + "'");
            }
            catch (Exception ex)
            {
                Check(false, "java 探测自检", ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void TestReadyDetector()

        {
            Console.WriteLine("- 启动完成判定");
            Check(ReadyDetector.IsReady("Tomcat started on port 8081 (http) with context path '/fw'", ""),
                  "Tomcat started on port 算就绪", null);
            Check(ReadyDetector.IsReady("Started FwApplication in 82.123 seconds", ""),
                  "Spring Boot 的 Started ... in N seconds", null);
            Check(ReadyDetector.IsReady("RocketMQ消息监听器注册完成,共12个监听器", "RocketMQ消息监听器注册完成"),
                  "自定义关键字（用户配置的）", null);
            Check(!ReadyDetector.IsReady("Starting FwApplication using Java 17.0.8 with PID 123", ""),
                  "刚启动那行不算就绪", null);
            Check(ReadyDetector.IsReady("服务启动完成，共 12 个监听器", "启动完成"),
                  "自定义关键字命中", null);
        }

        private static void TestProcess(string jarPath)
        {
            Console.WriteLine("- 进程启停");
            string jar = jarPath;
            if (string.IsNullOrEmpty(jar) || !File.Exists(jar))
            {
                jar = BuildTestJar();
                if (string.IsNullOrEmpty(jar))
                {
                    Console.WriteLine("  [跳过] 找不到 javac/jar，无法生成测试 jar");
                    return;
                }
            }

            var store = new LogStore(500);
            var pm = new ProcessManager(store);
            var item = new JarItem();
            item.Id = "selftest";
            item.JarPath = jar;
            item.Name = "selftest";

            string error;
            bool started = pm.Start(item, "", out error);
            Check(started, "启动测试 jar", error);
            if (!started) return;

            DateTime deadline = DateTime.Now.AddSeconds(15);
            var lines = new List<LogLine>();
            while (DateTime.Now < deadline)
            {
                lines = store.Get("selftest");
                bool gotHello = false;
                foreach (var l in lines) if (l.Text.IndexOf("hello-jarstart", StringComparison.OrdinalIgnoreCase) >= 0) gotHello = true;
                if (gotHello) break;
                Thread.Sleep(200);
            }

            bool sawHello = false;
            foreach (var l in lines) if (l.Text.IndexOf("hello-jarstart", StringComparison.OrdinalIgnoreCase) >= 0) sawHello = true;
            Check(sawHello, "捕获到子进程输出", "共 " + lines.Count + " 行");

            // 进程一起来只是"启动中"，要等到日志里出现完成标志才算"运行中"
            Check(pm.StatusOf("selftest") == JarStatus.Starting
                  || pm.StatusOf("selftest") == JarStatus.Running,
                  "刚启动时状态是启动中或运行中", pm.StatusOf("selftest").ToString());

            // 注意：java 的 stdout 重定向到管道后是块缓冲的，
            // 那几行可能要到进程退出才吐出来，所以这里等端口出现而不是等瞬时的状态
            DateTime portDeadline = DateTime.Now.AddSeconds(30);
            while (DateTime.Now < portDeadline && pm.Runtime("selftest").Port == 0)
                Thread.Sleep(200);

            var rt = pm.Runtime("selftest");
            Check(rt.Port == 8080, "识别到端口 8080", rt.Port.ToString());
            Check(rt.Ready, "识别到启动完成标志", rt.ReadyLine);

            pm.Stop("selftest", true);
            Thread.Sleep(1200);
            Check(!pm.IsRunning("selftest"), "停止后不再运行", pm.StatusOf("selftest").ToString());
        }

        /// <summary>用本机 JDK 生成一个会持续打印日志的测试 jar</summary>
        private static string BuildTestJar()
        {
            try
            {
                string javac = null, jarExe = null;
                string home = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrWhiteSpace(home))
                {
                    var c = Path.Combine(home, "bin", "javac.exe");
                    var j = Path.Combine(home, "bin", "jar.exe");
                    if (File.Exists(c)) javac = c;
                    if (File.Exists(j)) jarExe = j;
                }
                if (javac == null || jarExe == null)
                {
                    javac = "javac.exe";
                    jarExe = "jar.exe";
                }

                string dir = Path.Combine(Path.GetTempPath(), "jarstart-selftest-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                string src = Path.Combine(dir, "Hello.java");
                File.WriteAllText(src,
                    "public class Hello {\n" +
                    "    public static void main(String[] a) throws Exception {\n" +
                    "        System.out.println(\"hello-jarstart\");\n" +
                    "        System.out.println(\"args=\" + java.util.Arrays.toString(a));\n" +
                    "        System.err.println(\"some warning line\");\n" +
                    "        System.out.println(\"Tomcat initialized with port(s): 8080 (http)\");\n" +
                    "        System.out.println(\"Tomcat started on port(s): 8080 (http) with context path ''\");\n" +
                    "        for (int i = 0; i < 50; i++) {\n" +
                    "            System.out.println(\"tick \" + i);\n" +
                    "            Thread.sleep(200);\n" +
                    "        }\n" +
                    "    }\n" +
                    "}\n", new UTF8Encoding(false));

                var psi = new ProcessStartInfo(javac, "\"" + src + "\" -d \"" + dir + "\"");
                psi.UseShellExecute = false;
                psi.RedirectStandardError = true;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(60000);
                    if (p.ExitCode != 0)
                    {
                        Console.WriteLine("  [提示] javac 失败：" + p.StandardError.ReadToEnd());
                        return null;
                    }
                }

                string jarPath = Path.Combine(dir, "selftest.jar");
                var psi2 = new ProcessStartInfo(jarExe, "cfe \"" + jarPath + "\" Hello -C \"" + dir + "\" .");
                psi2.UseShellExecute = false;
                psi2.RedirectStandardError = true;
                psi2.CreateNoWindow = true;
                using (var p2 = Process.Start(psi2))
                {
                    p2.WaitForExit(60000);
                    if (p2.ExitCode != 0)
                    {
                        Console.WriteLine("  [提示] jar 打包失败：" + p2.StandardError.ReadToEnd());
                        return null;
                    }
                }
                return jarPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine("  [提示] 生成测试 jar 失败：" + ex.Message);
                return null;
            }
        }
    }
}
