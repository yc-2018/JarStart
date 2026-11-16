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

        // 选区按"字符"记：锚点 + 焦点，行列都是字符级，可以只框住一行里的几个字
        private int _anchorLine = -1;
        private int _anchorChar = -1;
        private int _focusLine = -1;
        private int _focusChar = -1;

        private bool _autoScroll = true;

        private bool _dragThumb;
        private bool _dragHThumb;
        private bool _selecting;            // 正在用鼠标拖选（不依赖 e.Button）
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
            _anchorLine = -1;
            _anchorChar = -1;
            _focusLine = -1;
            _focusChar = -1;
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
            _anchorLine = -1;
            _anchorChar = -1;
            _focusLine = -1;
            _focusChar = -1;
            Invalidate();
        }

        public string GetAllText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var l in _lines)
                sb.AppendLine(l.TimeText + "  " + l.Text);
            return sb.ToString();
        }

        /// <summary>复制出来的选区文本：整行选中的带上时间戳，局部选中的只取文字部分</summary>
        public string GetSelectedText()
        {
            int l1, c1, l2, c2;
            NormalizeSel(out l1, out c1, out l2, out c2);
            if (l1 < 0 || l2 < 0) return "";

            var sb = new System.Text.StringBuilder();
            for (int i = l1; i <= l2 && i < _lines.Count; i++)
            {
                string text = _lines[i].Text ?? "";
                int s = (i == l1) ? Math.Min(c1, text.Length) : 0;
                int e = (i == l2) ? Math.Min(c2, text.Length) : text.Length;
                if (s < 0) s = 0;
                if (e < 0) e = 0;
                if (e < s) e = s;
                bool whole = (s == 0 && e == text.Length);
                if (whole) sb.Append(_lines[i].TimeText + "  ");
                sb.AppendLine(text.Substring(s, e - s));
            }
            return sb.ToString();
        }

        /// <summary>把选区整理成"从小到大"的顺序</summary>
        private void NormalizeSel(out int l1, out int c1, out int l2, out int c2)
        {
            l1 = _anchorLine; c1 = _anchorChar; l2 = _focusLine; c2 = _focusChar;
            if (l1 < 0 || l2 < 0) { l1 = c1 = l2 = c2 = -1; return; }
            if (l1 > l2 || (l1 == l2 && c1 > c2))
            {
                int tl = l1; int tc = c1;
                l1 = l2; c1 = c2;
                l2 = tl; c2 = tc;
            }
        }

        private bool HasSelection { get { return _anchorLine >= 0 && _focusLine >= 0; } }

        /// <summary>量一段文字的宽度</summary>
        private float WidthOf(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    return g.MeasureString(s, Font).Width;
            }
            catch { return 0; }
        }

        /// <summary>把鼠标横坐标换算成这一行里的第几个字符</summary>
        private int CharAt(float x, int line)
        {
            if (line < 0 || line >= _lines.Count) return 0;
            string text = _lines[line].Text ?? "";
            if (text.Length == 0) return 0;

            float target = x - (TimeWidth - _hOffset);
            if (target <= 0) return 0;

            // 二分查找：宽度随字符数单调递增
            int lo = 0, hi = text.Length;
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        if (g.MeasureString(text.Substring(0, mid), Font).Width <= target) lo = mid;
                        else hi = mid - 1;
                    }
                }
            }
            catch { return 0; }
            return lo;
        }

        /// <summary>算作"词"的字符（双击选词用）</summary>
        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '-'
                || c == ':' || c == '/' || c == '@';
        }

        /// <summary>取某个位置所在的行号</summary>
        private int LineAtRow(float y)
        {
            int row = (int)(y / Math.Max(1, RowHeight));
            return _scrollLine + row;
        }

        // ——— 供自检使用 ———

        /// <summary>横坐标换算成字符下标（自检用）</summary>
        public int CharIndexAt(float x, int line) { return CharAt(x, line); }

        /// <summary>直接设定选区（自检用）</summary>
        public void SetSelection(int line, int startChar, int endChar)
        {
            _anchorLine = line; _anchorChar = startChar;
            _focusLine = line; _focusChar = endChar;
        }

        /// <summary>当前选区描述（自检用）</summary>
        public string SelectionInfo
        {
            get
            {
                int l1, c1, l2, c2;
                NormalizeSel(out l1, out c1, out l2, out c2);
                return l1 + "," + c1 + " -> " + l2 + "," + c2;
            }
        }

        /// <summary>当前滚动到的行（自检用）</summary>
        public int ScrollLine { get { return _scrollLine; } }

        /// <summary>鼠标事件计数（自检用）</summary>
        public int MouseDownCount { get; private set; }
        public int MouseMoveCount { get; private set; }
        public int MouseDoubleClickCount { get; private set; }

        /// <summary>排版信息（自检用）</summary>
        public int RowHeightPx { get { return RowHeight; } }
        public int LineIndexAt(float y) { return LineAtRow(y); }
        public int LineTotal { get { return _lines.Count; } }

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

            PublishScroll();
        }

        /// <summary>把滚动位置写到窗口属性上，自动化测试用它确认滚动条真的动了</summary>
        private void PublishScroll()
        {
            try
            {
                var form = FindForm();
                if (form != null && form.IsHandleCreated)
                    Native.SetProp(form.Handle, "JarStart.LogScroll", _scrollLine);
            }
            catch { }
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

            // 选中区域背景（按字符范围，可以只框住一行里的几个字）
            PaintSelection(g, rowH, timeW);

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

        /// <summary>按字符范围画选中高亮</summary>
        private void PaintSelection(Graphics g, int rowH, float timeW)
        {
            int l1, c1, l2, c2;
            NormalizeSel(out l1, out c1, out l2, out c2);
            if (l1 < 0 || l2 < 0) return;

            var t = Theme.Current;
            using (var selBrush = new SolidBrush(t.Dark ? Color.FromArgb(48, 62, 88) : Color.FromArgb(216, 230, 255)))
            {
                int first = Math.Max(l1, _scrollLine);
                int last = Math.Min(l2, _scrollLine + VisibleRows);
                for (int i = first; i <= last; i++)
                {
                    string text = _lines[i].Text ?? "";
                    int s = (i == l1) ? Math.Min(c1, text.Length) : 0;
                    int e = (i == l2) ? Math.Min(c2, text.Length) : text.Length;
                    if (e <= s) continue;

                    float x1 = timeW - _hOffset + WidthOf(text.Substring(0, s));
                    float x2 = timeW - _hOffset + WidthOf(text.Substring(0, e));
                    int y = (i - _scrollLine) * rowH;
                    if (y > ContentHeight) break;
                    var rect = new RectangleF(x1, y, Math.Max(1f, x2 - x1), rowH);
                    g.FillRectangle(selBrush, rect);
                }
            }
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
            MouseDownCount++;
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
                    _dragStartOffset = e.Y - thumbY;   // 记住抓在滑块的哪个位置
                    _autoScroll = false;
                    Capture = true;
                }
                else
                {
                    // 点击轨道：翻一页
                    bool below = e.Y > thumbY + thumbH;
                    _scrollLine = Math.Max(0, Math.Min(maxScroll, _scrollLine + (below ? VisibleRows : -VisibleRows)));
                    _autoScroll = false;
                    Invalidate();
                }
                return;
            }

            // 水平滚动条
            if (e.Y >= trackY && e.X < trackX)
            {
                float maxW = MaxContentWidth + TimeWidth;
                int trackW = Width - ScrollBarSize;
                int maxOffset = (int)Math.Max(0, maxW - trackW);
                if (maxOffset > 0)
                {
                    float ratio = trackW / Math.Max(1f, maxW);
                    int thumbW = Math.Max(24, (int)(trackW * ratio));
                    int thumbX = (int)((trackW - thumbW) * ((float)_hOffset / maxOffset));
                    if (e.X >= thumbX && e.X <= thumbX + thumbW)
                    {
                        _dragHThumb = true;
                        _dragStartOffset = e.X - thumbX;   // 抓取偏移（原来错用了 _hOffset，一拖就跳到头）
                        Capture = true;
                    }
                    else
                    {
                        // 点击轨道：左右翻一屏
                        bool right = e.X > thumbX + thumbW;
                        _hOffset = Math.Max(0, Math.Min(maxOffset, _hOffset + (right ? trackW : -trackW)));
                        Invalidate();
                    }
                }
                return;
            }

            // 正文：开始选择（按字符定位）
            int line = LineAtRow(e.Location.Y);
            if (line >= 0 && line < _lines.Count)
            {
                int ch = CharAt(e.Location.X, line);
                _anchorLine = line; _anchorChar = ch;
                _focusLine = line; _focusChar = ch;
                _selecting = true;
                Capture = true;
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            MouseDoubleClickCount++;
            if (e == null) return;
            if (e.X >= Width - ScrollBarSize && e.Y >= Height - ScrollBarSize) return;

            int line = LineAtRow(e.Location.Y);
            if (line < 0 || line >= _lines.Count) return;
            string text = _lines[line].Text ?? "";
            int ch = CharAt(e.Location.X, line);
            if (text.Length == 0) return;

            // 双击选中一个"词"
            int s = ch, e2 = ch;
            if (ch >= text.Length) ch = text.Length - 1;
            if (ch < 0) return;
            if (!IsWordChar(text[ch])) { s = ch; e2 = ch + 1; }
            else
            {
                s = ch; e2 = ch + 1;
                while (s > 0 && IsWordChar(text[s - 1])) s--;
                while (e2 < text.Length && IsWordChar(text[e2])) e2++;
            }
            _anchorLine = line; _anchorChar = s;
            _focusLine = line; _focusChar = e2;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            MouseMoveCount++;
            UpdateCursor(e);

            if (_dragThumb)
            {
                int maxScroll = Math.Max(1, _lines.Count - VisibleRows);
                float ratio = (float)VisibleRows / _lines.Count;
                int thumbH = Math.Max(24, (int)(ContentHeight * ratio));
                int travel = Math.Max(1, ContentHeight - thumbH);
                // 必须用浮点除法：整数除法会让滑块一拖就直接跳到最底
                float f = (e.Y - _dragStartOffset) / (float)travel;
                if (f < 0f) f = 0f;
                if (f > 1f) f = 1f;
                _scrollLine = (int)Math.Round(f * maxScroll);
                _autoScroll = false;
                PublishScroll();
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
                int travel = Math.Max(1, trackW - thumbW);
                float f = (e.X - _dragStartOffset) / (float)travel;
                if (f < 0f) f = 0f;
                if (f > 1f) f = 1f;
                _hOffset = (int)Math.Round(f * maxOffset);
                Invalidate();
                return;
            }

            if (_selecting)
            {
                // 不能靠 e.Button 判断：拖出控件再拖回来时按钮状态会丢，
                // 这里自己记着"正在选择"的状态
                int line = LineAtRow(e.Location.Y);
                if (line >= 0 && line < _lines.Count)
                {
                    _focusLine = line;
                    _focusChar = CharAt(e.Location.X, line);
                    Invalidate();
                }
                return;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragThumb = false;
            _dragHThumb = false;
            _selecting = false;
            if (Capture) Capture = false;
        }

        /// <summary>鼠标移到滚动条上时光标要变，别还是文本框那个竖线</summary>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.IBeam;
        }

        private void UpdateCursor(MouseEventArgs e)
        {
            int trackX = Width - ScrollBarSize;
            int trackY = Height - ScrollBarSize;
            bool overV = e.X >= trackX && e.Y < trackY;
            bool overH = e.Y >= trackY && e.X < trackX;

            if (overV || overH)
            {
                // 竖直滚动条上下、水平滚动条左右，光标要跟着变
                Cursor = overV ? Cursors.SizeNS : Cursors.SizeWE;
                return;
            }

            // 滑块上用手型，轨道上用箭头
            if (overV && _lines.Count > VisibleRows)
            {
                float ratio = (float)VisibleRows / _lines.Count;
                int thumbH = Math.Max(24, (int)(ContentHeight * ratio));
                int maxScroll = Math.Max(1, _lines.Count - VisibleRows);
                int thumbY = (int)((ContentHeight - thumbH) * ((float)_scrollLine / maxScroll));
                Cursor = (e.Y >= thumbY && e.Y <= thumbY + thumbH) ? Cursors.Hand : Cursors.Default;
                return;
            }
            if (overH)
            {
                float maxW = MaxContentWidth + TimeWidth;
                int trackW = Width - ScrollBarSize;
                int maxOffset = (int)Math.Max(0, maxW - trackW);
                if (maxOffset > 0)
                {
                    float ratio = trackW / Math.Max(1f, maxW);
                    int thumbW = Math.Max(24, (int)(trackW * ratio));
                    int thumbX = (int)((trackW - thumbW) * ((float)_hOffset / maxOffset));
                    Cursor = (e.X >= thumbX && e.X <= thumbX + thumbW) ? Cursors.Hand : Cursors.Default;
                    return;
                }
            }
            Cursor = Cursors.IBeam;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture)
            {
                _dragThumb = false;
                _dragHThumb = false;
                _selecting = false;
            }
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
                        _anchorLine = 0; _anchorChar = 0;
                        _focusLine = _lines.Count - 1;
                        _focusChar = (_lines[_lines.Count - 1].Text ?? "").Length;
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
