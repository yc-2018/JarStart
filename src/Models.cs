using System;
using System.Collections.Generic;
using System.IO;

namespace JarStart
{
    /// <summary>jar 进程状态</summary>
    public enum JarStatus
    {
        Stopped = 0,
        Starting = 1,
        Running = 2,
        Exited = 3,      // 已退出（可能是正常结束）
        Failed = 4,      // 启动失败
    }

    /// <summary>日志级别</summary>
    public enum LogLevel
    {
        Out = 0,         // 子进程标准输出
        Err = 1,         // 子进程标准错误
        Sys = 2,         // 启动器自身提示
    }

    /// <summary>一个 jar 包的配置</summary>
    public class JarItem
    {
        public string Id = "";
        public string Name = "";
        public string JarPath = "";
        public string Group = "";          // 空表示未分组
        public string JavaPath = "";       // 可空：留空则用环境变量里的 java
        public string JvmArgs = "";
        public string ProgramArgs = "";
        public string WorkDir = "";        // 空表示取 jar 所在目录
        public string EnvVars = "";        // 每行一条 KEY=VALUE
        public string Encoding = "UTF-8";  // UTF-8 / GBK
        public string Port = "";
        public bool AutoStart = false;     // 启动器启动时自动运行

        public JarItem Clone()
        {
            return new JarItem
            {
                Id = Id,
                Name = Name,
                JarPath = JarPath,
                Group = Group,
                JavaPath = JavaPath,
                JvmArgs = JvmArgs,
                ProgramArgs = ProgramArgs,
                WorkDir = WorkDir,
                EnvVars = EnvVars,
                Encoding = Encoding,
                Port = Port,
                AutoStart = AutoStart,
            };
        }

        /// <summary>展示用的名称：没设置就取 jar 文件名</summary>
        [System.Web.Script.Serialization.ScriptIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Name)) return Name;
                if (!string.IsNullOrWhiteSpace(JarPath))
                {
                    try { return Path.GetFileNameWithoutExtension(JarPath); }
                    catch { return JarPath; }
                }
                return "未命名";
            }
        }

        /// <summary>jar 文件是否还在</summary>
        [System.Web.Script.Serialization.ScriptIgnore]
        public bool JarExists
        {
            get
            {
                try { return !string.IsNullOrWhiteSpace(JarPath) && File.Exists(JarPath); }
                catch { return false; }
            }
        }
    }

    /// <summary>一行日志</summary>
    public class LogLine
    {
        public DateTime Time;
        public LogLevel Level;
        public string Text;

        public LogLine(DateTime time, LogLevel level, string text)
        {
            Time = time;
            Level = level;
            Text = text ?? "";
        }

        public string TimeText
        {
            get { return Time.ToString("HH:mm:ss"); }
        }
    }

    /// <summary>分组的展示信息</summary>
    public class GroupInfo
    {
        public string Name;
        public int Count;

        public GroupInfo(string name, int count)
        {
            Name = name;
            Count = count;
        }
    }

    public static class JarStatusText
    {
        public static string Describe(JarStatus s)
        {
            switch (s)
            {
                case JarStatus.Starting: return "启动中";
                case JarStatus.Running: return "运行中";
                case JarStatus.Exited: return "已退出";
                case JarStatus.Failed: return "启动失败";
                default: return "已停止";
            }
        }
    }
}
