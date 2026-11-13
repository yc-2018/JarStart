using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace IsolatedUi
{
    /// <summary>
    /// 界面自动化辅助：程序窗口放到屏幕外（靠配置里的 WinX 负数），
    /// 全程用 PostMessage 发消息驱动控件，不移动真实鼠标、不抢焦点。
    /// </summary>
    public class IsolatedUi
    {
        public class WinInfo
        {
            public IntPtr Handle;
            public string Title;
            public string ClassName;
            public bool Visible;
            public int X, Y, Width, Height;
        }

        private const uint STARTF_USESHOWWINDOW = 0x00000001;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessW(string appName, string cmdLine, IntPtr pa, IntPtr ta,
            bool inherit, uint flags, IntPtr env, string curDir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr lParam);
        private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder s, int n);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageW(IntPtr hWnd, int msg, IntPtr wp, string lp);
        [DllImport("user32.dll")]
        private static extern IntPtr PostMessageW(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
        [DllImport("kernel32.dll")]
        private static extern bool TerminateProcess(IntPtr hProcess, uint code);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr h);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId;
        }

        private IntPtr _process = IntPtr.Zero;

        public int ProcessId { get; private set; }
        public string LastError { get; private set; }

        public IsolatedUi(string exe, string args)
        {
            LastError = "";
            try
            {
                var si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
                si.dwFlags = (int)STARTF_USESHOWWINDOW;
                si.wShowWindow = 1;

                PROCESS_INFORMATION pi;
                if (!CreateProcessW(exe, args ?? "", IntPtr.Zero, IntPtr.Zero, false, 0,
                                   IntPtr.Zero, null, ref si, out pi))
                {
                    LastError = "CreateProcess 失败: " + Marshal.GetLastWin32Error();
                    return;
                }
                _process = pi.hProcess;
                ProcessId = pi.dwProcessId;
            }
            catch (Exception ex) { LastError = ex.Message; }
        }

        /// <summary>找出进程的所有顶层窗口</summary>
        public List<WinInfo> TopWindows()
        {
            var list = new List<WinInfo>();
            EnumWindows(delegate(IntPtr h, IntPtr p)
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == (uint)ProcessId)
                {
                    var sb = new System.Text.StringBuilder(512);
                    GetWindowText(h, sb, 512);
                    var cb = new System.Text.StringBuilder(256);
                    GetClassName(h, cb, 256);
                    RECT r;
                    GetWindowRect(h, out r);
                    var info = new WinInfo();
                    info.Handle = h;
                    info.Title = sb.ToString();
                    info.ClassName = cb.ToString();
                    info.Visible = IsWindowVisible(h);
                    info.X = r.Left; info.Y = r.Top;
                    info.Width = r.Right - r.Left; info.Height = r.Bottom - r.Top;
                    list.Add(info);
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        /// <summary>递归找出某个窗口下的所有子控件（含文字、类名、位置）</summary>
        public List<WinInfo> Children(IntPtr parent)
        {
            var list = new List<WinInfo>();
            EnumChildWindows(parent, delegate(IntPtr h, IntPtr p)
            {
                var sb = new System.Text.StringBuilder(512);
                GetWindowText(h, sb, 512);
                var cb = new System.Text.StringBuilder(256);
                GetClassName(h, cb, 256);
                RECT r;
                GetWindowRect(h, out r);
                var info = new WinInfo();
                info.Handle = h;
                info.Title = sb.ToString();
                info.ClassName = cb.ToString();
                info.Visible = IsWindowVisible(h);
                info.X = r.Left; info.Y = r.Top;
                info.Width = r.Right - r.Left; info.Height = r.Bottom - r.Top;
                list.Add(info);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        /// <summary>在控件上点击（用消息，不动真实鼠标）</summary>
        public void Click(IntPtr hwnd, int x, int y)
        {
            int lp = (y << 16) | (x & 0xFFFF);
            PostMessageW(hwnd, 0x0201 /*WM_LBUTTONDOWN*/, (IntPtr)1, (IntPtr)lp);
            Thread.Sleep(40);
            PostMessageW(hwnd, 0x0202 /*WM_LBUTTONUP*/, (IntPtr)0, (IntPtr)lp);
            Thread.Sleep(300);
        }

        /// <summary>右键</summary>
        public void RightClick(IntPtr hwnd, int x, int y)
        {
            int lp = (y << 16) | (x & 0xFFFF);
            PostMessageW(hwnd, 0x0204 /*WM_RBUTTONDOWN*/, (IntPtr)2, (IntPtr)lp);
            Thread.Sleep(40);
            PostMessageW(hwnd, 0x0205 /*WM_RBUTTONUP*/, (IntPtr)0, (IntPtr)lp);
            Thread.Sleep(400);
        }

        /// <summary>给输入框设置文字</summary>
        public void SetText(IntPtr hwnd, string text)
        {
            SendMessageW(hwnd, 0x000C /*WM_SETTEXT*/, IntPtr.Zero, text);
            Thread.Sleep(150);
        }

        /// <summary>给窗口发按键（不动真实键盘）</summary>
        public void Key(IntPtr hwnd, int vk)
        {
            PostMessageW(hwnd, 0x0100 /*WM_KEYDOWN*/, (IntPtr)vk, IntPtr.Zero);
            Thread.Sleep(30);
            PostMessageW(hwnd, 0x0101 /*WM_KEYUP*/, (IntPtr)vk, IntPtr.Zero);
            Thread.Sleep(250);
        }

        /// <summary>给窗口发一条自定义消息</summary>
        public void Message(IntPtr hwnd, int msg)
        {
            PostMessageW(hwnd, msg, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(400);
        }

        /// <summary>截窗口（PrintWindow，窗口在屏幕外/被遮挡都能截到）</summary>
        public void Capture(IntPtr hwnd, string path)
        {
            RECT r;
            GetWindowRect(hwnd, out r);
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w <= 0 || h <= 0) return;
            using (var bmp = new Bitmap(w, h))
            using (var g = Graphics.FromImage(bmp))
            {
                IntPtr hdc = g.GetHdc();
                PrintWindow(hwnd, hdc, 2 /*PW_RENDERFULLCONTENT*/);
                g.ReleaseHdc(hdc);
                bmp.Save(path, ImageFormat.Png);
            }
        }

        public void Kill()
        {
            if (_process != IntPtr.Zero)
            {
                TerminateProcess(_process, 0);
                CloseHandle(_process);
                _process = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Kill();
        }
    }
}
