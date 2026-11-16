using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>
    /// 程序图标。
    ///
    /// 优先用打包进 exe 的 assets/app.ico（由 assets/app.svg 转出，多尺寸 PNG 压缩），
    /// 这样任务栏、托盘、Alt+Tab 都是矢量原稿的清晰版本；
    /// 资源不在时退回运行时绘制，保证任何情况下都有图标。
    /// </summary>
    public static class AppIcon
    {
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

        private const string ResourceName = "JarStart.app.ico";

        private static readonly Dictionary<int, Icon> _cache = new Dictionary<int, Icon>();
        private static Icon _fallback;
        private static bool _fallbackDark;

        /// <summary>取指定像素尺寸的图标（会自动缓存）</summary>
        public static Icon Get(int size)
        {
            if (size < 16) size = 16;
            Icon ic;
            if (_cache.TryGetValue(size, out ic) && ic != null) return ic;

            ic = FromResource(size);
            if (ic == null) ic = BuildFallback(Theme.Current.Dark ? Color.FromArgb(98, 155, 255) : Color.FromArgb(79, 140, 255));
            _cache[size] = ic;
            return ic;
        }

        /// <summary>深浅色都走同一张图（红罐子图标两种主题下都好看）</summary>
        public static Icon Get(bool dark)
        {
            return Get(SmallIconSize());
        }

        /// <summary>按当前 DPI 算出托盘该用多大</summary>
        public static int SmallIconSize()
        {
            try
            {
                int s = SystemInformation.SmallIconSize.Width;
                if (s < 16) s = 16;
                if (s > 32) s = 32;
                return s;
            }
            catch { return 16; }
        }

        /// <summary>按当前 DPI 算出窗口图标该用多大</summary>
        public static int LargeIconSize()
        {
            try
            {
                // 任务栏图标在 100% 下是 32px，缩放比例上去后按比例放大
                int s = (int)Math.Round(32 * Ui.Scale);
                if (s < 32) s = 32;
                if (s > 64) s = 64;
                return s;
            }
            catch { return 32; }
        }

        // ——— 从嵌入资源里取 ———

        private static byte[] _icoBytes;

        private static byte[] IcoBytes
        {
            get
            {
                if (_icoBytes != null) return _icoBytes;
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    using (var s = asm.GetManifestResourceStream(ResourceName))
                    {
                        if (s == null) return null;
                        using (var ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            _icoBytes = ms.ToArray();
                        }
                    }
                }
                catch { }
                return _icoBytes;
            }
        }

        /// <summary>解析 ICO 目录，取出指定尺寸那一帧的 PNG 数据</summary>
        private static byte[] ExtractPng(int size)
        {
            var d = IcoBytes;
            if (d == null || d.Length < 6) return null;
            try
            {
                int count = d[4] | (d[5] << 8);
                int best = -1, bestSize = 0;
                for (int i = 0; i < count; i++)
                {
                    int off = 6 + i * 16;
                    if (off + 16 > d.Length) break;
                    int w = d[off] == 0 ? 256 : d[off];
                    int h = d[off + 1] == 0 ? 256 : d[off + 1];
                    int len = d[off + 8] | (d[off + 9] << 8) | (d[off + 10] << 16) | (d[off + 11] << 24);
                    int pos = d[off + 12] | (d[off + 13] << 8) | (d[off + 14] << 16) | (d[off + 15] << 24);
                    if (pos < 0 || pos + len > d.Length) continue;
                    // 必须是 PNG 帧（PNG 压缩的 ICO 才能直接当图片用）
                    if (!(d[pos] == 0x89 && d[pos + 1] == 'P' && d[pos + 2] == 'N' && d[pos + 3] == 'G')) continue;

                    int cur = w > h ? w : h;
                    if (cur == size) { best = pos; bestSize = len; break; }
                    if (best < 0) { best = pos; bestSize = len; }
                }
                if (best < 0) return null;
                var buf = new byte[bestSize];
                Array.Copy(d, best, buf, 0, bestSize);
                return buf;
            }
            catch { return null; }
        }

        private static Icon FromResource(int size)
        {
            try
            {
                var png = ExtractPng(size);
                if (png == null) return null;

                using (var ms = new MemoryStream(png))
                using (var bmp = new Bitmap(ms))
                {
                    IntPtr h = bmp.GetHicon();
                    try
                    {
                        using (var tmp = Icon.FromHandle(h))
                            return (Icon)tmp.Clone();     // 克隆后才能释放句柄
                    }
                    finally { DestroyIcon(h); }
                }
            }
            catch { return null; }
        }

        // ——— 兜底：运行时绘制 ———

        private static Icon BuildFallback(Color accent)
        {
            if (_fallback != null && _fallbackDark == (accent.R < 100)) return _fallback;
            _fallback = Build(accent);
            _fallbackDark = accent.R < 100;
            return _fallback;
        }

        public static Bitmap Draw(int size, Color accent)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                float r = size * 0.24f;
                using (var path = Rounded(new RectangleF(0, 0, size - 1, size - 1), r))
                using (var brush = new LinearGradientBrush(
                    new RectangleF(0, 0, size, size),
                    Lighten(accent, 0.18f), Darken(accent, 0.12f), 55f))
                {
                    g.FillPath(brush, path);
                }

                // 小尺寸下用 "J"，大尺寸用“启”，兼顾清晰度
                var glyph = size >= 24 ? "启" : "J";
                var fontName = size >= 24 ? "Microsoft YaHei UI" : "Segoe UI";
                using (var f = new Font(fontName, size * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    var rect = new RectangleF(0, size * 0.04f, size, size);
                    using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                        g.DrawString(glyph, f, shadow, new RectangleF(0.6f, size * 0.04f + 0.6f, size, size), sf);
                    g.DrawString(glyph, f, Brushes.White, rect, sf);
                }
            }
            return bmp;
        }

        private static Icon Build(Color accent)
        {
            using (var bmp = Draw(32, accent))
            {
                var h = bmp.GetHicon();
                try
                {
                    using (var tmp = Icon.FromHandle(h))
                        return (Icon)tmp.Clone();     // 克隆后即可释放句柄
                }
                finally { DestroyIcon(h); }
            }
        }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Lighten(Color c, float k)
        {
            return Color.FromArgb(c.A,
                (int)Math.Min(255, c.R + 255 * k),
                (int)Math.Min(255, c.G + 255 * k),
                (int)Math.Min(255, c.B + 255 * k));
        }

        public static Color Darken(Color c, float k)
        {
            return Color.FromArgb(c.A,
                (int)Math.Max(0, c.R - 255 * k),
                (int)Math.Max(0, c.G - 255 * k),
                (int)Math.Max(0, c.B - 255 * k));
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }
}
