using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>树控件的一次命中结果</summary>
    public class TreeHit
    {
        public bool IsGroup;
        public string GroupName;
        public string JarId;
    }

    /// <summary>
    /// 左侧的 jar 分组树：分组头 + 条目 + 状态点，全部自绘，保持 Win11 的观感。
    /// </summary>
    public class JarTreePanel : Panel
    {
        public const string AllGroup = "全部";
        public const string UngroupedGroup = "未分组";

        private static int HeaderHeight { get { return Ui.Px(30); } }
        private static int ItemHeight { get { return Ui.Px(46); } }
        private static int Indent { get { return Ui.Px(12); } }

        private class Row
        {
            public bool IsGroup;
            public string GroupName;
            public JarItem Item;
            public int Top;
            public int Height;
        }

        private AppConfig _cfg;
        private ProcessManager _pm;
        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<string, bool> _collapsed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly VScrollBar _vbar;
        private string _selectedId = "";
        private string _filter = "";

        public event Action<string> SelectionChanged;
        public event Action<string> ItemActivated;
        public event Action<TreeHit> ContextRequested;

        public JarTreePanel(AppConfig cfg, ProcessManager pm)
        {
            _cfg = cfg;
            _pm = pm;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint
                   | ControlStyles.Selectable, true);
            BackColor = Theme.Current.Panel;
            TabStop = true;
            Cursor = Cursors.Default;

            _vbar = new VScrollBar();
            _vbar.Scroll += delegate { Invalidate(); };
            _vbar.ValueChanged += delegate { Invalidate(); };
            Controls.Add(_vbar);
        }

        public void SetConfig(AppConfig cfg) { _cfg = cfg; }

        public string SelectedId
        {
            get { return _selectedId; }
            set
            {
                if (_selectedId == value) return;
                _selectedId = value ?? "";
                Invalidate();
                var h = SelectionChanged;
                if (h != null) h(_selectedId);
            }
        }

        public string Filter
        {
            get { return _filter; }
            set
            {
                _filter = value ?? "";
                Rebuild();
                Invalidate();
            }
        }

        public void Rebuild()
        {
            _rows.Clear();
            if (_cfg == null) return;

            bool filtering = !string.IsNullOrWhiteSpace(_filter);

            AppendGroup(AllGroup, CollectAll(), filtering);
            AppendGroup(UngroupedGroup, CollectIn(null), filtering);
            foreach (var g in _cfg.Groups)
                AppendGroup(g, CollectIn(g), filtering);

            UpdateScrollBar();
        }

        private List<JarItem> CollectAll()
        {
            var list = new List<JarItem>();
            foreach (var it in _cfg.Items) if (Matches(it)) list.Add(it);
            return list;
        }

        private List<JarItem> CollectIn(string group)
        {
            var list = new List<JarItem>();
            foreach (var it in _cfg.Items)
            {
                if (!Matches(it)) continue;
                if (string.IsNullOrEmpty(group))
                {
                    if (string.IsNullOrEmpty(it.Group)) list.Add(it);
                }
                else if (string.Equals(it.Group, group, StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(it);
                }
            }
            return list;
        }

        private bool Matches(JarItem it)
        {
            if (string.IsNullOrWhiteSpace(_filter)) return true;
            string f = _filter.Trim();
            if (it.DisplayName.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(it.JarPath) &&
                it.JarPath.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(it.Group) &&
                it.Group.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void AppendGroup(string name, List<JarItem> items, bool filtering)
        {
            if (items.Count == 0 && filtering) return;

            var head = new Row();
            head.IsGroup = true;
            head.GroupName = name;
            head.Height = HeaderHeight;
            _rows.Add(head);

            bool collapsed = IsCollapsed(name);
            if (collapsed) return;

            foreach (var it in items)
            {
                var row = new Row();
                row.IsGroup = false;
                row.Item = it;
                row.Height = ItemHeight;
                _rows.Add(row);
            }
        }

        private bool IsCollapsed(string name)
        {
            bool v;
            return _collapsed.TryGetValue(name, out v) && v;
        }

        public void ToggleGroup(string name)
        {
            bool v = !IsCollapsed(name);
            _collapsed[name] = v;
            Rebuild();
            Invalidate();
        }

        public bool IsGroupCollapsed(string name)
        {
            return IsCollapsed(name);
        }

        private void UpdateScrollBar()
        {
            int content = 0;
            foreach (var r in _rows) content += r.Height;
            int viewport = Height;
            bool need = content > viewport;

            _vbar.Visible = need;
            _vbar.SetBounds(Width - Ui.Px(12), 0, Ui.Px(12), Height);
            if (need)
            {
                int max = Math.Max(1, content - viewport + 1);
                _vbar.Maximum = max;
                _vbar.LargeChange = Math.Max(Ui.Px(20), viewport);
                _vbar.SmallChange = Ui.Px(20);
                if (_vbar.Value > max) _vbar.Value = max;
            }
            else
            {
                _vbar.Value = 0;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBar();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var t = Theme.Current;
            g.Clear(t.Panel);

            int offset = _vbar.Visible ? _vbar.Value : 0;
            int top = 0;
            foreach (var row in _rows)
            {
                row.Top = top;
                top += row.Height;
            }

            var nameFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            var subFont = new Font("Microsoft YaHei UI", 8f);
            var groupFont = new Font("Microsoft YaHei UI", 9f);
            try
            {
                int y = -offset;
                foreach (var row in _rows)
                {
                    if (y + row.Height > 0 && y < Height)
                    {
                        if (row.IsGroup) PaintGroup(g, row, y, groupFont);
                        else PaintItem(g, row, y, nameFont, subFont);
                    }
                    y += row.Height;
                }
            }
            finally
            {
                nameFont.Dispose();
                subFont.Dispose();
                groupFont.Dispose();
            }
        }

        private void PaintGroup(Graphics g, Row row, int y, Font font)
        {
            var t = Theme.Current;
            using (var b = new SolidBrush(t.SubText))
                g.DrawString(row.GroupName, font, b, Indent + Ui.Px(16), y + Ui.Px(7));

            // 数量徽标
            int count = CountIn(row.GroupName);

            string badge = count.ToString();
            SizeF sz = g.MeasureString(badge, font);
            var badgeRect = new RectangleF(Width - Ui.Px(34) - sz.Width, y + Ui.Px(6), sz.Width + Ui.Px(12), Ui.Px(18));
            using (var path = AppIcon.Rounded(badgeRect, Ui.Radius(8)))
            using (var b = new SolidBrush(t.Card))
                g.FillPath(b, path);
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(t.SubText))
                g.DrawString(badge, font, b, badgeRect, sf);

            // 展开箭头
            bool collapsed = IsCollapsed(row.GroupName);
            using (var p = new Pen(t.SubText, 1.6f * (float)Ui.Scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cx = Indent + Ui.Px(5), cy = y + HeaderHeight / 2f;
                if (collapsed)
                    g.DrawLines(p, new PointF[] { new PointF(cx - Ui.Px(2.5f), cy - Ui.Px(4f)), new PointF(cx + Ui.Px(2.5f), cy), new PointF(cx - Ui.Px(2.5f), cy + Ui.Px(4f)) });
                else
                    g.DrawLines(p, new PointF[] { new PointF(cx - Ui.Px(4f), cy - Ui.Px(2.5f)), new PointF(cx, cy + Ui.Px(2.5f)), new PointF(cx + Ui.Px(4f), cy - Ui.Px(2.5f)) });
            }

            using (var p = new Pen(t.Border, 1f * (float)Ui.Scale))
                g.DrawLine(p, Indent, y + HeaderHeight - Ui.Px(1), Width - Ui.Px(4), y + HeaderHeight - Ui.Px(1));
        }

        private int CountIn(string groupName)
        {
            int n = 0;
            if (_cfg == null) return 0;
            if (groupName == AllGroup)
            {
                foreach (var it in _cfg.Items) if (Matches(it)) n++;
                return n;
            }
            if (groupName == UngroupedGroup)
            {
                foreach (var it in _cfg.Items) if (Matches(it) && string.IsNullOrEmpty(it.Group)) n++;
                return n;
            }
            foreach (var it in _cfg.Items)
                if (Matches(it) && string.Equals(it.Group, groupName, StringComparison.OrdinalIgnoreCase)) n++;
            return n;
        }

        private void PaintItem(Graphics g, Row row, int y, Font nameFont, Font subFont)
        {
            var t = Theme.Current;
            var it = row.Item;
            bool selected = !string.IsNullOrEmpty(_selectedId) &&
                            string.Equals(_selectedId, it.Id, StringComparison.OrdinalIgnoreCase);

            int left = Indent + Ui.Px(8);
            int right = Width - (Ui.Px(8) + (_vbar.Visible ? Ui.Px(12) : 0));

            if (selected)
            {
                var sel = new RectangleF(left - Ui.Px(6), y + Ui.Px(3), right - left + Ui.Px(6), ItemHeight - Ui.Px(6));
                using (var path = AppIcon.Rounded(sel, Ui.Radius(6)))
                using (var b = new SolidBrush(t.SelBg))
                    g.FillPath(b, path);
            }

            // 状态点
            var status = _pm != null ? _pm.StatusOf(it.Id) : JarStatus.Stopped;
            Color dot = StatusColor(status, it);
            float dx = left + Ui.Px(6), dy = y + ItemHeight / 2f - Ui.Px(4);
            using (var b = new SolidBrush(dot))
                g.FillEllipse(b, dx, dy, Ui.Px(8), Ui.Px(8));
            if (!it.JarExists)
            {
                using (var p = new Pen(t.Danger, 1.6f * (float)Ui.Scale))
                    g.DrawEllipse(p, dx - Ui.Px(2.5f), dy - Ui.Px(2.5f), Ui.Px(13), Ui.Px(13));
            }

            float textLeft = dx + Ui.Px(17);
            float textRight = right - Ui.Px(6);
            if (textRight - textLeft < Ui.Px(40)) textRight = textLeft + Ui.Px(40);

            using (var sf = new StringFormat(StringFormatFlags.NoWrap)
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            })
            using (var b = new SolidBrush(t.Text))
                g.DrawString(it.DisplayName, nameFont, b,
                    new RectangleF(textLeft, y + Ui.Px(7), textRight - textLeft, Ui.Px(18)), sf);

            string sub = SubText(it, status);
            using (var sf = new StringFormat(StringFormatFlags.NoWrap)
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            })
            using (var b = new SolidBrush(t.SubText))
                g.DrawString(sub, subFont, b,
                    new RectangleF(textLeft, y + Ui.Px(25), textRight - textLeft, Ui.Px(16)), sf);
        }

        private string SubText(JarItem it, JarStatus status)
        {
            if (!it.JarExists) return "文件不存在：" + it.JarPath;
            string name = "";
            try { name = System.IO.Path.GetFileName(it.JarPath); } catch { }
            if (status == JarStatus.Running)
            {
                var rt = _pm.Runtime(it.Id);
                string pid = rt.Pid > 0 ? " · PID " + rt.Pid : "";
                string port = PortOf(it) > 0 ? " · 端口 " + PortOf(it) : "";
                return name + pid + port;
            }
            if (status == JarStatus.Failed) return name + " · 启动失败";
            if (status == JarStatus.Exited) return name + " · 已退出";
            return name;
        }

        /// <summary>端口：优先用启动日志里识别出来的，没有就用手动填的</summary>
        public int PortOf(JarItem it)
        {
            if (it == null) return 0;
            try
            {
                if (_pm != null)
                {
                    int detected = _pm.Runtime(it.Id).Port;
                    if (detected > 0) return detected;
                }
                int manual;
                if (!string.IsNullOrWhiteSpace(it.Port) && int.TryParse(it.Port.Trim(), out manual))
                    return manual;
            }
            catch { }
            return 0;
        }

        private Color StatusColor(JarStatus status, JarItem it)
        {
            var t = Theme.Current;
            switch (status)
            {
                case JarStatus.Running: return t.Success;
                case JarStatus.Starting: return t.Accent;
                case JarStatus.Exited: return t.Warning;
                case JarStatus.Failed: return t.Danger;
                default: return Color.FromArgb(160, 165, 175);
            }
        }

        private TreeHit HitAt(Point p)
        {
            int offset = _vbar.Visible ? _vbar.Value : 0;
            int y = p.Y + offset;
            foreach (var row in _rows)
            {
                if (y >= row.Top && y < row.Top + row.Height)
                {
                    var hit = new TreeHit();
                    hit.IsGroup = row.IsGroup;
                    hit.GroupName = row.GroupName;
                    hit.JarId = row.IsGroup ? null : row.Item.Id;
                    return hit;
                }
            }
            return null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            var hit = HitAt(e.Location);
            if (hit == null) return;

            if (e.Button == MouseButtons.Right)
            {
                if (!hit.IsGroup && !string.IsNullOrEmpty(hit.JarId))
                {
                    if (!string.Equals(_selectedId, hit.JarId, StringComparison.OrdinalIgnoreCase))
                        SelectedId = hit.JarId;
                }
                var h = ContextRequested;
                if (h != null) h(hit);
                return;
            }

            if (hit.IsGroup)
            {
                ToggleGroup(hit.GroupName);
            }
            else if (!string.IsNullOrEmpty(hit.JarId))
            {
                SelectedId = hit.JarId;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            var hit = HitAt(e.Location);
            if (hit == null || hit.IsGroup || string.IsNullOrEmpty(hit.JarId)) return;
            var h = ItemActivated;
            if (h != null) h(hit.JarId);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                MoveSelection(e.KeyCode == Keys.Down ? 1 : -1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (!string.IsNullOrEmpty(_selectedId))
                {
                    var h = ItemActivated;
                    if (h != null) h(_selectedId);
                }
                e.Handled = true;
            }
        }

        private void MoveSelection(int delta)
        {
            var ids = new List<string>();
            foreach (var r in _rows) if (!r.IsGroup) ids.Add(r.Item.Id);
            if (ids.Count == 0) return;
            int idx = ids.IndexOf(_selectedId);
            idx = idx < 0 ? 0 : idx + delta;
            if (idx < 0) idx = 0;
            if (idx >= ids.Count) idx = ids.Count - 1;
            SelectedId = ids[idx];
            EnsureVisible(ids[idx]);
        }

        public void EnsureVisible(string jarId)
        {
            foreach (var r in _rows)
            {
                if (r.IsGroup || r.Item.Id != jarId) continue;
                int offset = _vbar.Visible ? _vbar.Value : 0;
                if (r.Top < offset)
                {
                    _vbar.Value = Math.Max(0, r.Top);
                }
                else if (r.Top + r.Height > offset + Height)
                {
                    _vbar.Value = Math.Min(_vbar.Maximum, r.Top + r.Height - Height);
                }
                Invalidate();
                return;
            }
        }
    }
}
