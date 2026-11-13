using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace JarStart
{
    /// <summary>
    /// DPI 缩放：字体用「磅」写会自动跟着系统缩放，但控件位置尺寸是写死的像素，
    /// 所以布局用的数值统一乘上缩放系数，否则高 DPI 下就会出现文字被截断、控件被挤扁。
    /// </summary>
    public static class Ui
    {
        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        private static float _scale = 1f;

        /// <summary>当前缩放系数（1 = 100%）</summary>
        public static float Scale { get { return _scale; } }

        /// <summary>按桌面 DC 的 DPI 初始化（进程本身是 DPI 感知时拿到的就是真实 DPI）</summary>
        public static void Init()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    _scale = g.DpiX / 96f;
            }
            catch { _scale = 1f; }
            Clamp();
        }

        /// <summary>按窗口所在显示器的 DPI 初始化</summary>
        public static void InitFromWindow(IntPtr hwnd)
        {
            try
            {
                int dpi = GetDpiForWindow(hwnd);
                if (dpi > 0) { _scale = dpi / 96f; Clamp(); return; }
            }
            catch { }
            Init();
        }

        /// <summary>直接设置缩放系数（切换显示器时用）</summary>
        public static void SetScale(float scale)
        {
            _scale = scale;
            Clamp();
        }

        private static void Clamp()
        {
            if (_scale < 0.75f) _scale = 1f;
            if (_scale > 3f) _scale = 3f;
        }

        /// <summary>设计稿上的像素值换算成实际像素</summary>
        public static int Px(int v) { return (int)Math.Round(v * _scale); }

        public static int Px(float v) { return (int)Math.Round(v * _scale); }

        public static Size Sz(int w, int h) { return new Size(Px(w), Px(h)); }

        public static Point Pt(int x, int y) { return new Point(Px(x), Py(y)); }

        public static int Py(int v) { return (int)Math.Round(v * _scale); }

        /// <summary>常用圆角、留白等比例参数</summary>
        public static float Radius(float r) { return r * _scale; }
    }
}
