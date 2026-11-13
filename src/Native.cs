using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>Win32 互操作：DWM 圆角/深色标题栏、系统深色与强调色、窗口拖动</summary>
    public static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subApp, string subIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int CORNER_ROUND = 2;

        public static void ApplyRoundCorners(IntPtr hwnd)
        {
            int v = CORNER_ROUND;
            try { DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref v, sizeof(int)); } catch { }
        }

        public static void ApplyDarkTitleBar(IntPtr hwnd, bool dark)
        {
            int v = dark ? 1 : 0;
            try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int)); } catch { }
        }

        public static void ApplyBorderColor(IntPtr hwnd, Color c)
        {
            int v = c.R | (c.G << 8) | (c.B << 16);
            try { DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref v, sizeof(int)); } catch { }
        }

        /// <summary>让原生滚动条切换到深色/浅色外观</summary>
        public static void ApplyScrollbarTheme(Control c, bool dark)
        {
            try
            {
                if (c == null || !c.IsHandleCreated) return;
                SetWindowTheme(c.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch { }
        }

        /// <summary>系统是否为深色主题</summary>
        public static bool SystemUsesDarkTheme()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (k == null) return false;
                    var v = k.GetValue("AppsUseLightTheme");
                    return v != null && Convert.ToInt32(v) == 0;
                }
            }
            catch { return false; }
        }

        /// <summary>系统强调色，用作默认主题色</summary>
        public static Color? SystemAccent()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (k == null) return null;
                    var v = k.GetValue("AccentColor");
                    if (v == null) return null;
                    // 注册表里是 ABGR（无符号 32 位，值可能超过 int.MaxValue）
                    uint abgr = unchecked((uint)Convert.ToInt64(v));
                    return Color.FromArgb((int)(abgr & 0xFF), (int)((abgr >> 8) & 0xFF), (int)((abgr >> 16) & 0xFF));
                }
            }
            catch { return null; }
        }

        /// <summary>把窗口推到前台；必要时临时附加输入线程，否则 SetForegroundWindow 常常失败</summary>
        public static void ForceForeground(Form f)
        {
            try
            {
                IntPtr fore = GetForegroundWindow();
                uint foreThread = GetWindowThreadProcessId(fore, IntPtr.Zero);
                uint thisThread = GetCurrentThreadId();
                if (foreThread != thisThread) AttachThreadInput(foreThread, thisThread, true);
                ShowWindow(f.Handle, 5 /*SW_SHOW*/);
                SetForegroundWindow(f.Handle);
                f.Activate();
                if (foreThread != thisThread) AttachThreadInput(foreThread, thisThread, false);
            }
            catch { try { f.Activate(); } catch { } }
        }

        public static void SetDpiAware()
        {
            try
            {
                // Win10 1703+ 用 PerMonitorV2，失败则退回系统级 DPI 感知
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    if (!SetProcessDpiAwarenessContext((IntPtr)(-4))) SetProcessDPIAware();
                }
            }
            catch { }
        }
    }
}
