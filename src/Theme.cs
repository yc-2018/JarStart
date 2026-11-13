using System;
using System.Drawing;

namespace JarStart
{
    /// <summary>一套配色，随浅色/深色切换；控件直接读这里的字段</summary>
    public class Theme
    {
        public Color Bg, Panel, Card, Text, SubText, Border, Accent, AccentText, Hover;
        public Color Success, Warning, Danger, LogBg, LogText, LogErr, LogMuted, SelBg;
        public bool Dark;

        public static Theme Current = Light();

        public static Theme Light()
        {
            return new Theme
            {
                Dark = false,
                Bg = Color.FromArgb(249, 249, 251),
                Panel = Color.White,
                Card = Color.FromArgb(244, 245, 248),
                Text = Color.FromArgb(28, 30, 34),
                SubText = Color.FromArgb(120, 126, 138),
                Border = Color.FromArgb(226, 229, 235),
                Hover = Color.FromArgb(235, 238, 244),
                Accent = Color.FromArgb(79, 140, 255),
                AccentText = Color.White,
                Success = Color.FromArgb(31, 163, 90),
                Warning = Color.FromArgb(196, 143, 2),
                Danger = Color.FromArgb(209, 61, 61),
                LogBg = Color.FromArgb(252, 252, 253),
                LogText = Color.FromArgb(45, 48, 54),
                LogErr = Color.FromArgb(198, 46, 46),
                LogMuted = Color.FromArgb(140, 146, 158),
                SelBg = Color.FromArgb(226, 236, 255),
            };
        }

        public static Theme DarkTheme()
        {
            return new Theme
            {
                Dark = true,
                Bg = Color.FromArgb(32, 33, 36),
                Panel = Color.FromArgb(40, 42, 46),
                Card = Color.FromArgb(48, 50, 55),
                Text = Color.FromArgb(233, 235, 240),
                SubText = Color.FromArgb(150, 155, 165),
                Border = Color.FromArgb(58, 61, 67),
                Hover = Color.FromArgb(56, 59, 65),
                Accent = Color.FromArgb(98, 155, 255),
                AccentText = Color.White,
                Success = Color.FromArgb(64, 196, 120),
                Warning = Color.FromArgb(226, 178, 62),
                Danger = Color.FromArgb(235, 97, 97),
                LogBg = Color.FromArgb(27, 28, 31),
                LogText = Color.FromArgb(222, 226, 232),
                LogErr = Color.FromArgb(255, 123, 114),
                LogMuted = Color.FromArgb(120, 126, 138),
                SelBg = Color.FromArgb(48, 62, 88),
            };
        }

        public static void Apply(AppConfig c)
        {
            if (c == null) { Current = Light(); return; }
            bool dark = c.Theme == "dark" ||
                        (c.Theme == "auto" && Native.SystemUsesDarkTheme());
            Current = dark ? DarkTheme() : Light();

            var accent = ParseColor(c.Accent);
            if (accent.HasValue) Current.Accent = accent.Value;
            else
            {
                var sys = Native.SystemAccent();
                if (sys.HasValue) Current.Accent = sys.Value;
            }
        }

        public static Color? ParseColor(string hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return null;
                hex = hex.Trim().TrimStart('#');
                if (hex.Length != 6) return null;
                return Color.FromArgb(
                    Convert.ToInt32(hex.Substring(0, 2), 16),
                    Convert.ToInt32(hex.Substring(2, 2), 16),
                    Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            catch { return null; }
        }

        public static string ToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        /// <summary>找不到指定字体时退回微软雅黑，避免界面出现方框字</summary>
        public static string SafeFamily(string family, string fallback)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(family))
                {
                    using (var f = new Font(family, 10f))
                        if (f.Name.Equals(family, StringComparison.OrdinalIgnoreCase)) return family;
                }
            }
            catch { }
            return fallback;
        }
    }
}
