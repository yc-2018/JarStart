using System.Text.RegularExpressions;

namespace JarStart
{
    /// <summary>
    /// 去掉子进程输出里的 ANSI 颜色转义序列。
    /// logback 之类的日志框架即使输出被重定向也会带上 ESC[34m 这种东西，
    /// 直接显示的话日志里会全是乱码方块。
    /// </summary>
    public static class Ansi
    {
        private static readonly Regex Re = new Regex("\x1B\\[[0-9;?]*[A-Za-z]");

        public static string Strip(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.IndexOf('\x1B') < 0) return s;
            try { return Re.Replace(s, ""); }
            catch { return s; }
        }
    }
}
