using System;
using System.Drawing;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>系统托盘图标：最小化后还能找回窗口、快捷启停所有 jar</summary>
    public class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _menu;
        private bool _disposed;

        public event Action ShowRequested;
        public event Action ExitRequested;
        public event Action SettingsRequested;
        public event Action StartAllRequested;
        public event Action StopAllRequested;

        public TrayIcon(bool showIcon)
        {
            _menu = new ContextMenuStrip();
            _menu.Renderer = new MenuRenderer();
            _menu.Font = new Font("Microsoft YaHei UI", 9f);
            _menu.ShowImageMargin = false;

            var miShow = new ToolStripMenuItem("显示主窗口");
            miShow.Click += delegate { Fire(ShowRequested); };
            _menu.Items.Add(miShow);

            _menu.Items.Add(new ToolStripSeparator());

            var miStart = new ToolStripMenuItem("启动全部");
            miStart.Click += delegate { Fire(StartAllRequested); };
            _menu.Items.Add(miStart);

            var miStop = new ToolStripMenuItem("停止全部");
            miStop.Click += delegate { Fire(StopAllRequested); };
            _menu.Items.Add(miStop);

            _menu.Items.Add(new ToolStripSeparator());

            var miSettings = new ToolStripMenuItem("设置…");
            miSettings.Click += delegate { Fire(SettingsRequested); };
            _menu.Items.Add(miSettings);

            var miExit = new ToolStripMenuItem("退出");
            miExit.Click += delegate { Fire(ExitRequested); };
            _menu.Items.Add(miExit);

            _icon = new NotifyIcon();
            _icon.Icon = AppIcon.Get(Theme.Current.Dark);
            _icon.Text = "JarStart 启动器";
            _icon.ContextMenuStrip = _menu;
            // 自动化测试时可以不往系统托盘里放图标，避免在用户任务栏上留痕迹
            if (showIcon) _icon.Visible = true;
            _icon.DoubleClick += delegate { Fire(ShowRequested); };
        }

        private static void Fire(Action handler)
        {
            if (handler != null) handler();
        }

        /// <summary>主题切换后换图标</summary>
        public void UpdateTheme()
        {
            if (_disposed) return;
            try { _icon.Icon = AppIcon.Get(Theme.Current.Dark); }
            catch { }
        }

        /// <summary>冒个气泡提示</summary>
        public void Balloon(string title, string text)
        {
            if (_disposed) return;
            try
            {
                _icon.BalloonTipTitle = title;
                _icon.BalloonTipText = text;
                _icon.ShowBalloonTip(4000);
            }
            catch { }
        }

        /// <summary>把托盘菜单弹在指定位置（自动化测试用，可以弹到屏幕外）</summary>
        public void ShowMenuAt(Point screenPoint)
        {
            if (_disposed) return;
            try { _menu.Show(screenPoint); }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _icon.Visible = false; } catch { }
            try { _icon.Dispose(); } catch { }
            try { _menu.Dispose(); } catch { }
        }
    }
}
