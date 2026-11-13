using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace JarStart
{
    public partial class MainForm
    {
        private static int BarHeight { get { return Ui.Px(44); } }
        private static int FootHeight { get { return Ui.Px(26); } }
        private static int LeftBarHeight { get { return Ui.Px(40); } }

        private void Relayout()
        {
            if (_bar == null) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            int right = w;

            // 标题栏
            _bar.SetBounds(0, 0, w, BarHeight);
            _title.SetBounds(Ui.Px(18), 0, Math.Min(Ui.Px(320), w - Ui.Px(200)), BarHeight);
            _btnClose.SetBounds(right - Ui.Px(46), Ui.Px(7), Ui.Px(32), Ui.Px(28));
            _btnMin.SetBounds(right - Ui.Px(86), Ui.Px(7), Ui.Px(32), Ui.Px(28));
            _btnSettings.SetBounds(right - Ui.Px(126), Ui.Px(7), Ui.Px(32), Ui.Px(28));

            int contentTop = BarHeight;
            int contentBottom = h - FootHeight;
            int contentH = Math.Max(Ui.Px(60), contentBottom - contentTop);

            // 左侧
            _leftBar.SetBounds(0, contentTop, _leftWidth, LeftBarHeight);
            int ix = Ui.Px(10);
            _btnAdd.SetBounds(ix, Ui.Px(6), Ui.Px(28), Ui.Px(28)); ix += Ui.Px(30);
            _btnGroup.SetBounds(ix, Ui.Px(6), Ui.Px(28), Ui.Px(28)); ix += Ui.Px(30);
            _btnStart.SetBounds(ix, Ui.Px(6), Ui.Px(28), Ui.Px(28)); ix += Ui.Px(30);
            _btnStop.SetBounds(ix, Ui.Px(6), Ui.Px(28), Ui.Px(28)); ix += Ui.Px(30);
            _btnRemove.SetBounds(ix, Ui.Px(6), Ui.Px(28), Ui.Px(28));

            int searchY = contentTop + LeftBarHeight + Ui.Px(8);
            _search.SetBounds(Ui.Px(10), searchY, Math.Max(Ui.Px(60), _leftWidth - Ui.Px(20)), Ui.Px(30));

            int treeTop = searchY + Ui.Px(38);
            _tree.SetBounds(0, treeTop, _leftWidth, Math.Max(Ui.Px(40), contentBottom - treeTop));
            _splitter.SetBounds(_leftWidth, contentTop, Ui.Px(5), contentH);

            // 右侧
            _logPanel.SetBounds(_leftWidth + Ui.Px(5), contentTop, Math.Max(Ui.Px(60), w - _leftWidth - Ui.Px(5)), contentH);

            // 底部
            _foot.SetBounds(0, h - FootHeight, w, FootHeight);
            _status.SetBounds(Ui.Px(12), 0, Math.Max(Ui.Px(40), w - Ui.Px(24)), FootHeight);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Relayout();
        }

        public void ApplyTheme()
        {
            var t = Theme.Current;

            BackColor = t.Bg;
            Text = "JarStart 启动器";
            try { Icon = AppIcon.Get(t.Dark); } catch { }

            _bar.BackColor = t.Bg;
            _leftBar.BackColor = t.Bg;
            _foot.BackColor = t.Bg;
            _title.ForeColor = t.Text;
            _status.ForeColor = t.SubText;

            _tree.BackColor = t.Panel;
            _search.ApplyTheme();
            _logPanel.ApplyTheme();
            if (_tray != null) _tray.UpdateTheme();

            if (IsHandleCreated)
            {
                Native.ApplyRoundCorners(Handle);
                Native.ApplyDarkTitleBar(Handle, t.Dark);
                Native.ApplyBorderColor(Handle, t.Border);
            }

            Invalidate(true);
            Relayout();
            UpdateStatus();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ApplyRoundCorners(Handle);
            Native.ApplyDarkTitleBar(Handle, Theme.Current.Dark);
            Native.ApplyBorderColor(Handle, Theme.Current.Border);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;        // CS_DROPSHADOW：给无边框窗口一点投影
                return cp;
            }
        }

        private static float CornerRadius { get { return Ui.Radius(9f); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float inset = 0.5f * (float)Ui.Scale;
            using (var path = AppIcon.Rounded(
                new RectangleF(inset, inset, Width - 2 * inset, Height - 2 * inset), CornerRadius))
            using (var p = new Pen(Theme.Current.Border, 1.2f * (float)Ui.Scale))
                g.DrawPath(p, path);
        }

        private void Bar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Native.ReleaseCapture();
            Native.SendMessage(Handle, 0xA1 /*WM_NCLBUTTONDOWN*/, (IntPtr)2 /*HTCAPTION*/, IntPtr.Zero);
        }

        private void Splitter_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragStartX = Cursor.Position.X;
            _dragStartWidth = _leftWidth;
            Capture = true;
        }

        private void Splitter_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            int delta = Cursor.Position.X - _dragStartX;
            int width = _dragStartWidth + delta;
            int minW = Ui.Px(240), maxW = Ui.Px(520);
            if (width < minW) width = minW;
            if (width > maxW) width = maxW;
            if (width > ClientSize.Width - Ui.Px(260)) width = Math.Max(minW, ClientSize.Width - Ui.Px(260));
            if (width == _leftWidth) return;
            _leftWidth = width;
            Relayout();
        }

        private void Splitter_MouseUp(object sender, MouseEventArgs e)
        {
            _dragging = false;
            Capture = false;
        }

        /// <summary>无边框窗口的边缘缩放 + 标题栏拖动</summary>
        private int ResizeHitTest(Point p)
        {
            int grip = Ui.Px(6);
            bool l = p.X <= grip, r = p.X >= ClientSize.Width - grip;
            bool t = p.Y <= grip, b = p.Y >= ClientSize.Height - grip;

            if (l && t) return 13;
            if (r && t) return 14;
            if (l && b) return 16;
            if (r && b) return 17;
            if (l) return 10;
            if (r) return 11;
            if (t) return 12;
            if (b) return 15;

            // 标题栏区域交给系统处理（可拖动、可双击最大化）
            if (p.Y >= 0 && p.Y < BarHeight) return 2;

            return 1;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int WM_DPICHANGED = 0x02E0;

            // 自动化测试通道：收到这条消息就把托盘菜单弹出来（弹在窗口所在处，屏幕外）
            if (m.Msg == 0x8001)
            {
                try { if (_tray != null) _tray.ShowMenuAt(new Point(Left + 20, Top + 20)); }
                catch { }
                return;
            }

            if (m.Msg == WM_DPICHANGED)
            {
                // 拖到另一块缩放比例不同的显示器：按新 DPI 重新排版
                int dpi = (int)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                if (dpi > 0) Ui.SetScale(dpi / 96f);
                try
                {
                    var r = (Native.RECT)Marshal.PtrToStructure(m.LParam, typeof(Native.RECT));
                    SetBounds(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                }
                catch { }
                Relayout();
                UpdateStatus();
                return;
            }

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1 /*HTCLIENT*/)
                {
                    var p = PointToClient(new Point(m.LParam.ToInt32()));
                    m.Result = (IntPtr)ResizeHitTest(p);
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
}
