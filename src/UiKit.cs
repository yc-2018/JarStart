using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>扁平圆角按钮，支持主色实心、幽灵与危险三种样式</summary>
    public class FlatBtn : Control
    {
        public bool Primary;
        public bool Danger;
        public bool Compact;          // 小尺寸方形按钮
        private bool _hover, _down;

        public FlatBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
            Cursor = Cursors.Hand;
            Size = new Size(78, 30);
            Font = new Font("Microsoft YaHei UI", 9f);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float inset = 0.5f * (float)Ui.Scale;
            var rect = new RectangleF(inset, inset, Width - 2 * inset, Height - 2 * inset);
            Color fill, fore;

            if (Primary)
            {
                Color baseColor = Danger ? t.Danger : t.Accent;
                fill = _down ? AppIcon.Darken(baseColor, 0.08f)
                     : _hover ? AppIcon.Lighten(baseColor, 0.06f) : baseColor;
                fore = t.AccentText;
            }
            else
            {
                fill = _down ? t.Border : _hover ? t.Hover : Color.Transparent;
                fore = Danger ? t.Danger : t.Text;
            }
            if (!Enabled) { fill = Color.Transparent; fore = t.SubText; }

            if (fill != Color.Transparent)
            {
                using (var path = AppIcon.Rounded(rect, Ui.Radius(6)))
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            }

            using (var sf = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
            })
            using (var b = new SolidBrush(fore))
                g.DrawString(Text, Font, b, new RectangleF(0, 0, Width, Height), sf);
        }
    }

    /// <summary>细边框圆角卡片，用来包住原生文本框，使其呈现现代控件外观</summary>
    public class Card : Panel
    {
        public int Radius = 8;
        public bool Highlight;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);

            float inset = 0.5f * (float)Ui.Scale;
            var rect = new RectangleF(inset, inset, Width - 2 * inset, Height - 2 * inset);
            using (var path = AppIcon.Rounded(rect, Ui.Radius(Radius)))
            {
                using (var b = new SolidBrush(BackColor)) g.FillPath(b, path);
                using (var p = new Pen(Highlight ? t.Accent : t.Border,
                                       (Highlight ? 1.4f : 1f) * (float)Ui.Scale))
                    g.DrawPath(p, path);
            }
        }
    }

    /// <summary>矢量图标按钮，图形全部代码绘制</summary>
    public class IconBtn : Control
    {
        public string Kind = "close";      // close | min | gear | plus | trash | folder | play | stop | restart | copy | search | refresh | group | chevron
        public string Tip;
        private bool _hover, _down, _active;
        private ToolTip _toolTip;

        public bool Active
        {
            get { return _active; }
            set
            {
                if (_active == value) return;
                _active = value;
                Invalidate();
            }
        }

        public IconBtn()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);
            Size = Ui.Sz(32, 28);
            Cursor = Cursors.Hand;
        }

        public void SetTip(string tip)
        {
            Tip = tip;
            if (_toolTip == null && !string.IsNullOrWhiteSpace(tip))
            {
                _toolTip = new ToolTip();
                _toolTip.SetToolTip(this, tip);
            }
            else if (_toolTip != null)
            {
                _toolTip.SetToolTip(this, tip);
            }
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            if (!string.IsNullOrWhiteSpace(Tip) && _toolTip == null)
            {
                _toolTip = new ToolTip();
                _toolTip.SetToolTip(this, Tip);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _toolTip != null) { _toolTip.Dispose(); _toolTip = null; }
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);

            float cx = Width / 2f, cy = Height / 2f;

            if (_hover || _down || Active)
            {
                var bg = Kind == "close" ? Color.FromArgb(232, 66, 58)
                       : Active ? t.Accent
                       : (_down ? t.Border : t.Hover);
                using (var path = AppIcon.Rounded(new RectangleF(1, 1, Width - 3, Height - 3), Ui.Radius(5)))
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
            }

            var fg = (Kind == "close" && (_hover || _down)) ? Color.White
                   : Active ? t.AccentText : t.Text;

            // 图形本身按缩放系数放大，保证高 DPI 下图标不被挤在中间一小块
            var state = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform(Ui.Scale, Ui.Scale);
            g.TranslateTransform(-cx, -cy);

            using (var p = new Pen(fg, 1.5f * (float)Ui.Scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (Kind)
                {
                    case "close":
                        g.DrawLine(p, cx - 4, cy - 4, cx + 4, cy + 4);
                        g.DrawLine(p, cx + 4, cy - 4, cx - 4, cy + 4);
                        break;
                    case "min":
                        g.DrawLine(p, cx - 5, cy, cx + 5, cy);
                        break;
                    case "gear":
                        g.DrawLine(p, cx - 5.5f, cy - 3f, cx + 5.5f, cy - 3f);
                        g.DrawLine(p, cx - 5.5f, cy + 3f, cx + 5.5f, cy + 3f);
                        using (var dot = new SolidBrush(fg))
                        {
                            g.FillEllipse(dot, cx - 2.6f, cy - 5.6f, 5.2f, 5.2f);
                            g.FillEllipse(dot, cx + 0.4f, cy + 0.4f, 5.2f, 5.2f);
                        }
                        break;
                    case "plus":
                        g.DrawLine(p, cx - 5, cy, cx + 5, cy);
                        g.DrawLine(p, cx, cy - 5, cx, cy + 5);
                        break;
                    case "trash":
                        g.DrawLine(p, cx - 4.5f, cy - 3.5f, cx + 4.5f, cy - 3.5f);
                        g.DrawLine(p, cx - 3.2f, cy - 3.5f, cx - 2.7f, cy + 5f);
                        g.DrawLine(p, cx + 3.2f, cy - 3.5f, cx + 2.7f, cy + 5f);
                        g.DrawLine(p, cx - 4.5f, cy - 5.5f, cx + 4.5f, cy - 5.5f);
                        break;
                    case "folder":
                        g.DrawLine(p, cx - 5.5f, cy - 4.5f, cx - 1.5f, cy - 4.5f);
                        g.DrawLine(p, cx - 1.5f, cy - 4.5f, cx - 0.2f, cy - 2.8f);
                        g.DrawLine(p, cx - 0.2f, cy - 2.8f, cx + 5.5f, cy - 2.8f);
                        g.DrawLine(p, cx + 5.5f, cy - 2.8f, cx + 5.5f, cy + 4.5f);
                        g.DrawLine(p, cx + 5.5f, cy + 4.5f, cx - 5.5f, cy + 4.5f);
                        g.DrawLine(p, cx - 5.5f, cy + 4.5f, cx - 5.5f, cy - 2.8f);
                        break;
                    case "play":
                        g.DrawLines(p, new PointF[] {
                            new PointF(cx - 3.5f, cy - 5f), new PointF(cx + 5f, cy), new PointF(cx - 3.5f, cy + 5f) });
                        break;
                    case "stop":
                        g.DrawRectangle(p, cx - 4.2f, cy - 4.2f, 8.4f, 8.4f);
                        break;
                    case "restart":
                        g.DrawArc(p, cx, cy, 10f, 10f, 40, 260);
                        g.DrawLines(p, new PointF[] {
                            new PointF(cx + 4.6f, cy - 4.4f), new PointF(cx + 5.2f, cy + 0.6f), new PointF(cx + 0.4f, cy - 1.6f) });
                        break;
                    case "copy":
                        g.DrawRectangle(p, cx - 5.5f, cy - 5.5f, 7, 7);
                        g.DrawRectangle(p, cx - 1.5f, cy - 1.5f, 7, 7);
                        break;
                    case "search":
                        g.DrawEllipse(p, cx - 2.6f, cy - 4.6f, 8f, 8f);
                        g.DrawLine(p, cx + 3.4f, cy + 3.4f, cx + 5.8f, cy + 5.8f);
                        break;
                    case "refresh":
                        g.DrawArc(p, cx, cy, 9.5f, 9.5f, 30, 280);
                        g.DrawLines(p, new PointF[] {
                            new PointF(cx + 3.4f, cy - 5.2f), new PointF(cx + 4.6f, cy - 0.6f), new PointF(cx + 0.2f, cy - 1.4f) });
                        break;
                    case "group":
                        g.DrawRectangle(p, cx - 5.5f, cy - 5.5f, 11f, 4.5f);
                        g.DrawLine(p, cx - 4f, cy - 1f, cx - 4f, cy + 4.5f);
                        g.DrawLine(p, cx + 4f, cy - 1f, cx + 4f, cy + 4.5f);
                        break;
                    case "chevron":
                        g.DrawLines(p, new PointF[] {
                            new PointF(cx - 2f, cy - 4f), new PointF(cx + 2.5f, cy), new PointF(cx - 2f, cy + 4f) });
                        break;
                }
            }

            g.Restore(state);
        }
    }

    /// <summary>无边框输入框：外层卡片负责画边框，内部是原生 TextBox</summary>
    public class LineEdit : Panel
    {
        private readonly TextBox _box;
        private readonly Label _hint;

        public TextBox Box { get { return _box; } }

        /// <summary>输入框为空时显示的提示文字</summary>
        public string Placeholder
        {
            get { return _hint.Text; }
            set { _hint.Text = value ?? ""; _hint.Visible = string.IsNullOrEmpty(_box.Text) && _hint.Text.Length > 0; }
        }

        public LineEdit()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

            _box = new TextBox();
            _box.BorderStyle = BorderStyle.None;
            _box.BackColor = Theme.Current.Card;   // TextBox 不支持透明背景
            _box.Font = new Font("Microsoft YaHei UI", 9f);
            _box.TextChanged += delegate { SyncHint(); };
            Controls.Add(_box);

            // 提示文字作为子控件盖在输入框上方，避免透明背景在自绘父容器里不显示
            _hint = new Label();
            _hint.AutoSize = false;
            _hint.BackColor = Theme.Current.Card;
            _hint.ForeColor = Theme.Current.SubText;
            _hint.Font = new Font("Microsoft YaHei UI", 9f);
            _hint.TextAlign = ContentAlignment.MiddleLeft;
            _hint.Click += delegate { _box.Focus(); };
            Controls.Add(_hint);
            _hint.BringToFront();
        }

        private void SyncHint()
        {
            _hint.Visible = string.IsNullOrEmpty(_box.Text) && _hint.Text.Length > 0;
            Invalidate();
        }

        /// <summary>主题切换后同步输入框颜色</summary>
        public void ApplyTheme()
        {
            _box.BackColor = Theme.Current.Card;
            _box.ForeColor = Theme.Current.Text;
            _hint.BackColor = Theme.Current.Card;
            _hint.ForeColor = Theme.Current.SubText;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : t.Bg);
            float inset = 0.5f * (float)Ui.Scale;
            var rect = new RectangleF(inset, inset, Width - 2 * inset, Height - 2 * inset);
            using (var path = AppIcon.Rounded(rect, Ui.Radius(6)))
            {
                using (var b = new SolidBrush(t.Card)) g.FillPath(b, path);
                using (var p = new Pen(t.Border, 1f * (float)Ui.Scale)) g.DrawPath(p, path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int h = _box.PreferredHeight;
            int y = Math.Max(0, (Height - h) / 2);
            int pad = Ui.Px(8);
            _box.SetBounds(pad, y, Math.Max(10, Width - pad * 2), h);
            _hint.SetBounds(pad + Ui.Px(1), y, Math.Max(10, Width - pad * 2 - Ui.Px(2)), h);
        }
    }

    /// <summary>Win11 风格的右键菜单配色</summary>
    public class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() : base(new ProfessionalColorTable()) { }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(Theme.Current.Panel))
                e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Current.Text : Theme.Current.SubText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var p = new Pen(Theme.Current.Border))
                e.Graphics.DrawLine(p, 6, e.Item.Height / 2, e.Item.Width - 6, e.Item.Height / 2);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;
            using (var b = new SolidBrush(Theme.Current.Hover))
                e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
        }
    }

    /// <summary>单行文本输入的小对话框，用于新建/重命名分组</summary>
    public class TextPromptForm : Form
    {
        private readonly LineEdit _edit;
        public string Value { get { return _edit.Box.Text.Trim(); } }

        public TextPromptForm(string title, string prompt, string initial)
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = Ui.Sz(340, 116);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Theme.Current.Bg;
            Text = title;

            var label = new Label();
            label.Text = prompt;
            label.ForeColor = Theme.Current.SubText;
            label.AutoSize = false;
            label.SetBounds(Ui.Px(18), Ui.Px(16), Ui.Px(304), Ui.Px(20));

            _edit = new LineEdit();
            _edit.SetBounds(Ui.Px(18), Ui.Px(42), Ui.Px(304), Ui.Px(30));
            _edit.Box.Text = initial ?? "";

            var ok = new FlatBtn();
            ok.Text = "确定";
            ok.Primary = true;
            ok.SetBounds(Ui.Px(150), Ui.Px(82), Ui.Px(84), Ui.Px(28));
            ok.Click += delegate { Accept(); };

            var cancel = new FlatBtn();
            cancel.Text = "取消";
            cancel.SetBounds(Ui.Px(238), Ui.Px(82), Ui.Px(84), Ui.Px(28));
            cancel.Click += delegate { Cancel(); };

            Controls.Add(label);
            Controls.Add(_edit);
            Controls.Add(ok);
            Controls.Add(cancel);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { Cancel(); e.Handled = true; e.SuppressKeyPress = true; }
            };
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Enter) { Accept(); return true; }
            if (keyData == Keys.Escape) { Cancel(); return true; }
            return base.ProcessDialogKey(keyData);
        }

        private void Accept()
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void Cancel()
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ApplyDarkTitleBar(Handle, Theme.Current.Dark);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _edit.Box.Focus();
            _edit.Box.SelectAll();
        }
    }
}
