using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>
    /// 自绘日志视图：等宽字体、按级别着色、自带 Win11 风格的滚动条。
    /// 用它代替 RichTextBox，主要是为了在深色模式下滚动条也能保持一致的外观。
    /// </summary>
    public class LogView : Control
    {
        private static int ScrollBarSize { get { return Ui.Px(12); } }

        private readonly List<LogLine> _lines = new List<LogLine>();
        private readonly List<float> _widths = new List<float>();
        private int _scrollLine;
        private int _hOffset;
        private int _selStart = -1;
        private int _selEnd = -1;
        private bool _autoScroll = true;

        private bool _dragThumb;
        private bool _dragHThumb;
        private int _dragStartScroll;
        private int _dragStartOffset;
        private int _maxLines = 3000;

        public LogView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint
                   | ControlStyles.Selectable, true);
            BackColor = Theme.Current.LogBg;
            ForeColor = Theme.Current.LogText;
            Font = new Font("Consolas", 9.5f);
            TabStop = true;
            Cursor = Cursors.IBeam;
        }

        public bool AutoScrollEnabled
        {
            get { return _autoScroll; }
            set { _autoScroll = value; }
        }

        public void ApplyTheme(Font font)
        {
            BackColor = Theme.Current.LogBg;
            ForeColor = Theme.Current.LogText;
            if (font != null) { Font = font; }
            Invalidate();
        }

        private int RowHeight
        {
            // 用 Font.Height 直接算，避免每次绘制都去申请一个 Graphics（会漏 GDI 句柄）
            get { return (int)Math.Ceiling((double)Font.Height) + Ui.Px(3); }
        }

        private int ContentHeight
        {
            get { return Math.Max(0, Height - ScrollBarSize); }
        }

        private int VisibleRows
        {
            get { return Math.Max(1, ContentHeight / RowHeight); }
        }

        public int LineCount { get { return _lines.Count; } }

        /// <summary>最多保留的行数，超出后淘汰最早的</summary>
        public int MaxLines
        {
            get { return _maxLines; }
            set
            {
                _maxLines = value < 200 ? 200 : value;
                Trim();
            }
        }

        private void Trim()
        {
            if (_lines.Count <= _maxLines) return;
            int drop = _lines.Count - _maxLines / 2;
            if (drop <= 0) return;
            _lines.RemoveRange(0, drop);
            _widths.RemoveRange(0, drop);
            _scrollLine = Math.Max(0, _scrollLine - drop);
        }

        /// <summary>整体替换内容</summary>
        public void SetLines(List<LogLine> lines)
        {
            _lines.Clear();
            _widths.Clear();
            if (lines != null)
            {
                foreach (var l in lines)
                {
                    _lines.Add(l);
                    _widths.Add(MeasureWidth(l));
                }
            }
            _selStart = -1;
            _selEnd = -1;
            ClampScroll();
            Invalidate();
        }

        /// <summary>追加新行（自动滚到底部）</summary>
        public void AppendLines(List<LogLine> batch)
        {
            if (batch == null) return;
            foreach (var l in batch)
            {
                _lines.Add(l);
                _widths.Add(MeasureWidth(l));
            }
            Trim();
            if (_autoScroll) _scrollLine = Math.Max(0, _lines.Count - VisibleRows);
            else ClampScroll();
            Invalidate();
        }

        public void Clear()
        {
            _lines.Clear();
            _widths.Clear();
            _scrollLine = 0;
            _hOffset = 0;
            _selStart = -1;
            _selEnd = -1;
            Invalidate();
        }

        public string GetAllText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var l in _lines)
                sb.AppendLine(l.TimeText + "  " + l.Text);
            return sb.ToString();
        }

        public string GetSelectedText()
        {
            if (_selStart < 0 || _selEnd < 0) return "";
            int a = Math.Min(_selStart, _selEnd);
            int b = Math.Max(_selStart, _selEnd);
            var sb = new System.Text.StringBuilder();
            for (int i = a; i <= b && i < _lines.Count; i++)
                sb.AppendLine(_lines[i].TimeText + "  " + _lines[i].Text);
            return sb.ToString();
        }

        private float MeasureWidth(LogLine line)
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return g.MeasureString(line.TimeText + "  " + line.Text, Font).Width;
                }
            }
            catch { return 0; }
        }

        private float TimeWidth
        {
            get
            {
                try
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                        return g.MeasureString("00:00:00", Font).Width + Ui.Px(16);
                }
                catch { return 60; }
            }
        }

        private float MaxContentWidth
        {
            get
            {
                float max = 0;
                for (int i = 0; i < _widths.Count; i++) if (_widths[i] > max) max = _widths[i];
                return max;
            }
        }

        private void ClampScroll()
        {
            int max = Math.Max(0, _lines.Count - VisibleRows);
            if (_scrollLine > max) _scrollLine = max;
            if (_scrollLine < 0) _scrollLine = 0;

            float maxW = MaxContentWidth + TimeWidth;
            int maxOffset = (int)Math.Max(0, maxW - (Width - ScrollBarSize));
            if (_hOffset > maxOffset) _hOffset = maxOffset;
            if (_hOffset < 0) _hOffset = 0;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampScroll();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var t = Theme.Current;
            g.Clear(t.LogBg);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int rowH = RowHeight;
            int rows = VisibleRows;
            float timeW = TimeWidth;

            // 选中区域背景
            if (_selStart >= 0 && _selEnd >= 0)
            {
                int a = Math.Min(_selStart, _selEnd);
                int b = Math.Max(_selStart, _selEnd);
                int y1 = (a - _scrollLine) * rowH;
                int y2 = (b - _scrollLine + 1) * rowH;
                if (y2 > 0 && y1 < ContentHeight)
                {
                    var rect = new Rectangle(0, y1, Width - ScrollBarSize, y2 - y1);
                    using (var selBrush = new SolidBrush(t.Dark ? Color.FromArgb(48, 62, 88) : Color.FromArgb(216, 230, 255)))
                        g.FillRectangle(selBrush, rect);
                }
            }

            int count = Math.Min(_lines.Count, _scrollLine + rows + 1);
            for (int i = _scrollLine; i < count; i++)
            {
                int y = (i - _scrollLine) * rowH;
                if (y > ContentHeight) break;
                var line = _lines[i];

                Color timeColor = t.LogMuted;
                Color textColor = line.Level == LogLevel.Err ? t.LogErr
                                : line.Level == LogLevel.Sys ? t.Accent
                                : t.LogText;

                using (var b = new SolidBrush(timeColor))
                    g.DrawString(line.TimeText, Font, b, -_hOffset, y + Ui.Px(1));
                using (var b = new SolidBrush(textColor))
                    g.DrawString(line.Text, Font, b, timeW - _hOffset, y + Ui.Px(1));
            }

            DrawVerticalBar(g);
            DrawHorizontalBar(g);
        }

        private void DrawVerticalBar(Graphics g)
        {
            var t = Theme.Current;
            int trackX = Width - ScrollBarSize;
            int trackH = ContentHeight;
            if (_lines.Count <= VisibleRows) return;

            float ratio = (float)VisibleRows / _lines.Count;
            int thumbH = Math.Max(24, (int)(trackH * ratio));
            int maxScroll = Math.Max(1, _lines.Count - VisibleRows);
            int thumbY = (int)((trackH - thumbH) * ((float)_scrollLine / maxScroll));

            using (var path = AppIcon.Rounded(new RectangleF(trackX + Ui.Px(3), thumbY, ScrollBarSize - Ui.Px(6), thumbH), Ui.Radius(3)))
            using (var b = new SolidBrush(t.Dark ? Color.FromArgb(90, 94, 102) : Color.FromArgb(190, 194, 202)))
                g.FillPath(b, path);
        }

        private void DrawHorizontalBar(Graphics g)
        {
            var t = Theme.Current;
            int trackY = Height - ScrollBarSize;
            int trackW = Width - ScrollBarSize;
            float maxW = MaxContentWidth + TimeWidth;
            int maxOffset = (int)Math.Max(0, maxW - trackW);
            if (maxOffset <= 0) return;

            float ratio = (float)trackW / Math.Max(1f, maxW);
            int thumbW = Math.Max(24, (int)(trackW * ratio));
            int thumbX = (int)((trackW - thumbW) * ((float)_hOffset / maxOffset));

            using (var path = AppIcon.Rounded(new RectangleF(thumbX, trackY + Ui.Px(3), thumbW, ScrollBarSize - Ui.Px(6)), Ui.Radius(3)))
            using (var b = new SolidBrush(t.Dark ? Color.FromArgb(90, 94, 102) : Color.FromArgb(190, 194, 202)))
                g.FillPath(b, path);
        }

        private int LineAt(Point p)
        {
            int row = p.Y / Math.Max(1, RowHeight);
            return _scrollLine + row;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            int trackX = Width - ScrollBarSize;
            int trackY = Height - ScrollBarSize;

            // 垂直滚动条
            if (e.X >= trackX && e.Y < trackY && _lines.Count > VisibleRows)
            {
                float ratio = (float)VisibleRows / _lines.Count;
                int thumbH = Math.Max(24, (int)(ContentHeight * ratio));
                int maxScroll = Math.Max(1, _lines.Count - VisibleRows);
                int thumbY = (int)((ContentHeight - thumbH) * ((float)_scrollLine / maxScroll));
                if (e.Y >= thumbY && e.Y <= thumbY + thumbH)
                {
                    _dragThumb = true;
                    _dragStartScroll = _scrollLine;
                    _dragStartOffset = e.Y - thumbY;
                }
                else
                {
                    // 点击轨道：翻一页
                    bool below = e.Y > thumbY + thumbH;
                    _scrollLine = Math.Max(0, Math.Min(maxScroll, _scrollLine + (below ? VisibleRows : -VisibleRows)));
                    Invalidate();
                }
                return;
            }

            // 水平滚动条
            if (e.Y >= trackY && e.X < trackX)
            {
                _dragHThumb = true;
                _dragStartOffset = _hOffset;
                return;
            }

            // 正文：开始选择
            int line = LineAt(e.Location);
            if (line >= 0 && line < _lines.Count)
            {
                _selStart = line;
                _selEnd = line;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_dragThumb)
            {
                int maxScroll = Math.Max(1, _lines.Count - VisibleRows);
                float ratio = (float)VisibleRows / _lines.Count;
                int thumbH = Math.Max(24, (int)(ContentHeight * ratio));
                int pos = e.Y - _dragStartOffset;
                int scroll = (int)((pos / Math.Max(1, (ContentHeight - thumbH))) * maxScroll);
                _scrollLine = Math.Max(0, Math.Min(maxScroll, scroll));
                Invalidate();
                return;
            }

            if (_dragHThumb)
            {
                float maxW = MaxContentWidth + TimeWidth;
                int trackW = Width - ScrollBarSize;
                int maxOffset = (int)Math.Max(0, maxW - trackW);
                float ratio = (float)trackW / Math.Max(1f, maxW);
                int thumbW = Math.Max(24, (int)(trackW * ratio));
                int pos = e.X - _dragStartOffset;
                int offset = (int)((pos / Math.Max(1, (trackW - thumbW))) * maxOffset);
                _hOffset = Math.Max(0, Math.Min(maxOffset, offset));
                Invalidate();
                return;
            }

            if (e.Button == MouseButtons.Left && _selStart >= 0)
            {
                int line = LineAt(e.Location);
                if (line >= 0 && line < _lines.Count)
                {
                    _selEnd = line;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragThumb = false;
            _dragHThumb = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int delta = e.Delta / 120;
            if (delta == 0) delta = e.Delta > 0 ? 1 : -1;

            if ((ModifierKeys & Keys.Shift) != 0)
            {
                float maxW = MaxContentWidth + TimeWidth;
                int trackW = Width - ScrollBarSize;
                int maxOffset = (int)Math.Max(0, maxW - trackW);
                _hOffset = Math.Max(0, Math.Min(maxOffset, _hOffset - delta * 40));
            }
            else
            {
                int maxScroll = Math.Max(0, _lines.Count - VisibleRows);
                _scrollLine = Math.Max(0, Math.Min(maxScroll, _scrollLine - delta * 3));
                if (_scrollLine < maxScroll) _autoScroll = false;
            }
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.PageUp
                || keyData == Keys.PageDown || keyData == Keys.Home || keyData == Keys.End
                || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int maxScroll = Math.Max(0, _lines.Count - VisibleRows);
            switch (e.KeyCode)
            {
                case Keys.Up:
                    _scrollLine = Math.Max(0, _scrollLine - 1);
                    e.Handled = true;
                    break;
                case Keys.Down:
                    _scrollLine = Math.Min(maxScroll, _scrollLine + 1);
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                    _scrollLine = Math.Max(0, _scrollLine - VisibleRows);
                    e.Handled = true;
                    break;
                case Keys.PageDown:
                    _scrollLine = Math.Min(maxScroll, _scrollLine + VisibleRows);
                    e.Handled = true;
                    break;
                case Keys.Home:
                    _scrollLine = 0;
                    e.Handled = true;
                    break;
                case Keys.End:
                    _scrollLine = maxScroll;
                    e.Handled = true;
                    break;
                case Keys.A:
                    if ((ModifierKeys & Keys.Control) != 0 && _lines.Count > 0)
                    {
                        _selStart = 0;
                        _selEnd = _lines.Count - 1;
                        Invalidate();
                        e.Handled = true;
                    }
                    break;
                case Keys.C:
                    if ((ModifierKeys & Keys.Control) != 0)
                    {
                        try
                        {
                            var text = GetSelectedText();
                            if (text.Length == 0) text = GetAllText();
                            if (text.Length > 0) Clipboard.SetText(text);
                        }
                        catch { }
                        e.Handled = true;
                    }
                    break;
            }
        }
    }
}
