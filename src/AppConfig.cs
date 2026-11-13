using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace JarStart
{
    /// <summary>全局配置，序列化到 %APPDATA%\JarStart\config.json</summary>
    public class AppConfig
    {
        public string Version = "1";
        public List<JarItem> Items = new List<JarItem>();
        public List<string> Groups = new List<string>();

        // —— 外观 ——
        public string Theme = "auto";            // auto | light | dark
        public string Accent = "";               // 空表示跟随系统强调色
        public string LogFontFamily = "Cascadia Mono";
        public float LogFontSize = 9.5f;
        public bool AutoScroll = true;

        // —— 行为 ——
        public string DefaultJavaPath = "";      // 默认 java 路径（可空）
        public int MaxLogLines = 3000;           // 每个 jar 在内存里保留的日志行数
        public bool StopAllOnExit = true;        // 关闭启动器时停止所有子进程
        public bool MinimizeToTray = true;        // 点关闭时缩到托盘而不是退出
        public bool TrayHintShown = false;        // 是否已经提示过"缩到托盘"了
        public string LastSelectedId = "";

        // —— 窗口 ——
        public int WinX = -1, WinY = -1, WinW = 1180, WinH = 720;

        private static string Dir
        {
            get
            {
                // 自动化测试时可以用 JARSTART_CONFIG_DIR 指定单独的配置文件目录
                string custom = Environment.GetEnvironmentVariable("JARSTART_CONFIG_DIR");
                if (!string.IsNullOrWhiteSpace(custom)) return custom;
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "JarStart");
            }
        }

        public static string FilePath { get { return Path.Combine(Dir, "config.json"); } }

        public static AppConfig Load()
        {
            var cfg = new AppConfig();
            try
            {
                if (File.Exists(FilePath))
                {
                    var txt = File.ReadAllText(FilePath, Encoding.UTF8);
                    var loaded = new JavaScriptSerializer().Deserialize<AppConfig>(txt);
                    if (loaded != null) cfg = loaded;
                }
            }
            catch
            {
                // 配置损坏则回退默认值，不让程序起不来
                try
                {
                    var bad = Path.Combine(Dir, "config.broken.json");
                    if (File.Exists(FilePath)) File.Copy(FilePath, bad, true);
                }
                catch { }
                cfg = new AppConfig();
            }
            cfg.Normalize();
            return cfg;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var json = new JavaScriptSerializer().Serialize(this);
                // 先写临时文件再替换，避免写一半掉电损坏配置
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch { }
        }

        /// <summary>补齐缺失字段、去掉脏数据，保证界面拿到的是干净模型</summary>
        public void Normalize()
        {
            if (Items == null) Items = new List<JarItem>();
            if (Groups == null) Groups = new List<string>();
            if (MaxLogLines < 200) MaxLogLines = 200;
            if (MaxLogLines > 100000) MaxLogLines = 100000;
            if (LogFontSize < 7f) LogFontSize = 9.5f;
            if (LogFontSize > 20f) LogFontSize = 20f;
            if (string.IsNullOrWhiteSpace(LogFontFamily)) LogFontFamily = "Cascadia Mono";
            if (string.IsNullOrWhiteSpace(Theme)) Theme = "auto";

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<JarItem>();
            foreach (var it in Items)
            {
                if (it == null) continue;
                if (string.IsNullOrWhiteSpace(it.Id)) it.Id = Guid.NewGuid().ToString("N");
                while (!ids.Add(it.Id)) it.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(it.Encoding)) it.Encoding = "UTF-8";
                items.Add(it);
            }
            Items = items;

            var groups = new List<string>();
            foreach (var g in Groups)
            {
                if (string.IsNullOrWhiteSpace(g)) continue;
                if (groups.Contains(g)) continue;
                groups.Add(g);
            }
            Groups = groups;
        }
    }
}
