using System;
using System.Text.RegularExpressions;

namespace JarStart
{
    /// <summary>
    /// 判断服务是不是真的启动完成了。
    ///
    /// 进程一起来并不代表服务能用：像 fw 这种服务要跑一分多钟才把端口监听起来，
    /// 中间这段只能显示"启动中"。Spring Boot 标准的 "Started Xxx in N seconds"
    /// 很多框架并不会打印（比如 fw 就被框架自己屏蔽了），
    /// 所以这里准备了一批常见关键字，用户也能在设置里追加自己框架的标志行。
    /// </summary>
    public static class ReadyDetector
    {
        public static readonly string[] DefaultKeywords = new string[]
        {
            "Tomcat started on port",
            "Undertow started",
            "Netty started on port",
            "Jetty started",
            "started on port",
            "started successfully",
            "start success",
            "Application started",
            "is ready",
            "启动成功",
            "启动完成",
            "启动完毕",
            "服务启动成功",
            "服务已启动",
        };

        // Spring Boot：Started FwApplication in 82.123 seconds
        private static readonly Regex ReSpringStarted =
            new Regex(@"Started\s+\S+\s+in\s+[\d.]+\s+seconds", RegexOptions.IgnoreCase);

        /// <summary>这一行是不是"服务已就绪"的标志</summary>
        public static bool IsReady(string line, string extraKeywords)
        {
            if (string.IsNullOrEmpty(line)) return false;

            if (ReSpringStarted.IsMatch(line)) return true;

            for (int i = 0; i < DefaultKeywords.Length; i++)
            {
                if (line.IndexOf(DefaultKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            if (!string.IsNullOrWhiteSpace(extraKeywords))
            {
                var parts = extraKeywords.Split(new char[] { ',', '，', ';', '；', '\r', '\n' },
                                               StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    var k = parts[i].Trim();
                    if (k.Length == 0) continue;
                    if (line.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            return false;
        }
    }
}
