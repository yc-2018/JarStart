using System;
using System.Text.RegularExpressions;

namespace JarStart
{
    /// <summary>
    /// 从子进程的输出里识别它监听的端口。
    /// 常见写法：
    ///   Tomcat started on port(s): 8080 (http) with context path ''
    ///   Undertow started on port(s) 8080
    ///   Netty started on port(s): 8080
    ///   started at http://localhost:8080
    /// 只认这些明确的"启动/监听"语句，避免把日志里随便出现的数字当成端口。
    /// </summary>
    public static class PortDetector
    {
        // 注意：要用 ( \(s\) )? 这种写法让整个 "(s)" 可选。
        // 写成 port\(s\)? 的话，问号只作用于右括号，等于要求日志里必须有 "(s"，
        // Spring Boot 3.x 的 "Tomcat started on port 8081 (http)" 就永远匹配不上。
        // 端口用命名组抓，以后往正则里加括号也不会把组号搞乱。
        private static readonly Regex RePortWord = new Regex(
            @"port(\(s\))?\s*[:=]?\s*(?<port>\d{2,5})\b", RegexOptions.IgnoreCase);

        private static readonly Regex RePortEq = new Regex(
            @"\bport\s*[:=]\s*(?<port>\d{2,5})\b", RegexOptions.IgnoreCase);

        private static readonly Regex ReLocalUrl = new Regex(
            @"https?://(?:localhost|127\.0\.0\.1|\[::1\])(?::(?<port>\d{2,5}))?",
            RegexOptions.IgnoreCase);

        /// <summary>从一行输出里解析端口，识别不到返回 0</summary>
        public static int Detect(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;

            bool startup = Has(line, "start") || Has(line, "listen") || Has(line, "bind")
                        || Has(line, "serve") || Has(line, "running");

            if (startup)
            {
                int p = Match(RePortWord, line);
                if (p > 0) return p;
                p = Match(RePortEq, line);
                if (p > 0) return p;
            }

            // "started at http://localhost:8080" 这种写法
            var m = ReLocalUrl.Match(line);
            if (m.Success && startup && m.Groups["port"].Success)
            {
                int p;
                if (int.TryParse(m.Groups["port"].Value, out p) && p > 0 && p < 65536) return p;
            }
            return 0;
        }

        private static int Match(Regex re, string line)
        {
            var m = re.Match(line);
            if (!m.Success) return 0;
            int p;
            if (!int.TryParse(m.Groups["port"].Value, out p)) return 0;
            if (p <= 0 || p > 65535) return 0;
            return p;
        }

        private static bool Has(string s, string word)
        {
            return s.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
