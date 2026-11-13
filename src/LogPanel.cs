using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>右侧日志面板：工具条 + 过滤 + 日志正文</summary>
    public class LogPanel : UserControl
    {
        private AppConfig _cfg;
        private LogStore _store;
        private ProcessManager _pm;

        private Label _name;
        private Label _path;
        private Label _status;
        private ToolTip _statusTip;
        private FlatBtn _btnStart, _btnStop, _btnRestart, _btnClear, _btnCopy, _btnExport;
        private LineEdit _filter;
        private Card _card;
        private LogView _view;
        private Panel _head;

        private string _jarId = "";
        private readonly List<LogLine> _pending = new List<LogLine>();
        private readonly System.Windows.Forms.Timer _timer;

        public event Action StartRequested;
        public event Action StopRequested;
        public event Action RestartRequested;

        public LogPanel(AppConfig cfg, LogStore store, ProcessManager pm)
        {
            _cfg = cfg;
            _store = store;
            _pm = pm;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Theme.Current.Bg;
            DoubleBuffered = true;

            BuildUi();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 120;
            _timer.Tick += delegate { Flush(); };
            _timer.Start();

            _store.LineAppended += OnLineAppended;
            _pm.StatusChanged += delegate { BeginInvokeSafe(new Action(RefreshStatus)); };
        }

        private void BuildUi()
        {
            var t = Theme.Current;

            _name = new Label();
            _name.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            _name.AutoSize = false;
            _name.Text = "未选择 jar";

            _path = new Label();
            _path.Font = new Font("Microsoft YaHei UI", 8.5f);
            _path.AutoSize = false;
            _path.ForeColor = t.SubText;

            _status = new Label();
            _status.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Bold);
            _status.AutoSize = false;
            _status.TextAlign = ContentAlignment.MiddleCenter;
            _statusTip = new ToolTip();

            _btnStart = MakeBtn("启动", true, delegate { if (StartRequested != null) StartRequested(); });
            _btnStop = MakeBtn("停止", false, delegate { if (StopRequested != null) StopRequested(); });
            _btnRestart = MakeBtn("重启", false, delegate { if (RestartRequested != null) RestartRequested(); });
            _btnClear = MakeBtn("清空", false, delegate { ClearBox(); });
            _btnCopy = MakeBtn("复制", false, delegate { CopyAll(); });
            _btnExport = MakeBtn("导出", false, delegate { ExportAll(); });

            var head = new Panel();
            _head = head;
            head.BackColor = t.Bg;
            head.Controls.Add(_name);
            head.Controls.Add(_path);
            head.Controls.Add(_status);
            head.Controls.Add(_btnStart);
            head.Controls.Add(_btnStop);
            head.Controls.Add(_btnRestart);
            head.Controls.Add(_btnClear);
            head.Controls.Add(_btnCopy);
            head.Controls.Add(_btnExport);
            head.Height = 54;
            Controls.Add(head);

            _filter = new LineEdit();
            _filter.Box.TextChanged += delegate { RebuildFromStore(); };
            _filter.Height = 30;
            _filter.Placeholder = "过滤日志…（只显示包含关键字的行）";
            Controls.Add(_filter);

            _card = new Card();
            _card.BackColor = t.LogBg;

            _view = new LogView();
            _view.BackColor = t.LogBg;
            _card.Controls.Add(_view);
            Controls.Add(_card);

            LayoutControls();
        }

        private Font LogFont()
        {
            string fam = Theme.SafeFamily(_cfg != null ? _cfg.LogFontFamily : "Cascadia Mono", "Consolas");
            float size = _cfg != null ? _cfg.LogFontSize : 9.5f;
            try { return new Font(fam, size); }
            catch { return new Font("Consolas", 9.5f); }
        }

        private FlatBtn MakeBtn(string text, bool primary, EventHandler click)
        {
            var b = new FlatBtn();
            b.Text = text;
            b.Primary = primary;
            b.Size = Ui.Sz(58, 28);
            b.Font = new Font("Microsoft YaHei UI", 9f);
            b.Click += click;
            return b;
        }

        private void LayoutControls()
        {
            int w = Width, h = Height;
            int pad = Ui.Px(12);
            int right = w - pad;

            var head = _head;
            if (head != null) head.SetBounds(0, 0, w, Ui.Px(54));

            // 顶部一行：名称、状态、按钮
            int btnY = Ui.Px(10);
            _btnExport.Location = new Point(right - _btnExport.Width, btnY);
            _btnCopy.Location = new Point(_btnExport.Left - _btnCopy.Width - Ui.Px(4), btnY);
            _btnClear.Location = new Point(_btnCopy.Left - _btnClear.Width - Ui.Px(4), btnY);
            _btnRestart.Location = new Point(_btnClear.Left - _btnRestart.Width - Ui.Px(12), btnY);
            _btnStop.Location = new Point(_btnRestart.Left - _btnStop.Width - Ui.Px(4), btnY);
            _btnStart.Location = new Point(_btnStop.Left - _btnStart.Width - Ui.Px(4), btnY);

            int statusW = Ui.Px(96);
            _status.SetBounds(_btnStart.Left - statusW - Ui.Px(14), btnY + Ui.Px(3), statusW, Ui.Px(22));

            int textLeft = pad;
            int textRight = _status.Left - Ui.Px(10);
            if (textRight - textLeft < Ui.Px(80)) textRight = textLeft + Ui.Px(80);
            _name.SetBounds(textLeft, Ui.Px(8), textRight - textLeft, Ui.Px(22));
            _path.SetBounds(textLeft, Ui.Px(30), textRight - textLeft, Ui.Px(16));

            int filterY = Ui.Px(58);
            _filter.SetBounds(pad, filterY, Math.Max(Ui.Px(120), w - pad * 2), Ui.Px(30));

            int top = filterY + Ui.Px(30) + Ui.Px(10);
            _card.SetBounds(pad, top, w - pad * 2, Math.Max(Ui.Px(60), h - top - pad));

            const int inner = 8;
            int in2 = Ui.Px(inner);
            _view.SetBounds(in2, in2, Math.Max(10, _card.Width - in2 * 2), Math.Max(10, _card.Height - in2 * 2));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
        }

        public void ApplyTheme()
        {
            var t = Theme.Current;
            BackColor = t.Bg;
            _name.ForeColor = t.Text;
            _path.ForeColor = t.SubText;
            _card.BackColor = t.LogBg;
            _view.ApplyTheme(LogFont());
            _view.AutoScrollEnabled = _cfg == null || _cfg.AutoScroll;
            if (_cfg != null) _view.MaxLines = _cfg.MaxLogLines;
            Invalidate(true);
        }

        /// <summary>切换到某个 jar，载入它的内存日志</summary>
        public void SetJar(string jarId)
        {
            _jarId = jarId ?? "";
            lock (_pending) { _pending.Clear(); }
            RebuildFromStore();
            RefreshStatus();
        }

        public string CurrentJarId { get { return _jarId; } }

        public JarItem CurrentItem
        {
            get
            {
                if (_cfg == null || string.IsNullOrEmpty(_jarId)) return null;
                foreach (var it in _cfg.Items)
                    if (string.Equals(it.Id, _jarId, StringComparison.OrdinalIgnoreCase)) return it;
                return null;
            }
        }

        public void RefreshStatus()
        {
            var item = CurrentItem;
            var t = Theme.Current;
            if (item == null)
            {
                _name.Text = "未选择";
                _path.Text = "在左侧选择一个 jar 查看它的运行日志";
                _status.Text = "";
                _status.ForeColor = t.SubText;
                SetButtons(false, false);
                return;
            }

            _name.Text = item.DisplayName;
            _path.Text = item.JarPath;
            var status = _pm.StatusOf(item.Id);
            var rt = _pm.Runtime(item.Id);
            int port = rt.Port;
            if (port <= 0 && !string.IsNullOrWhiteSpace(item.Port))
                int.TryParse(item.Port.Trim(), out port);

            string text = JarStatusText.Describe(status);
            if (status == JarStatus.Running)
            {
                if (port > 0) text = text + " · " + port;
                else if (rt.Pid > 0) text = text + " " + rt.Pid;
            }
            _status.Text = text;
            string tip = "PID " + rt.Pid;
            if (port > 0) tip += " · 端口 " + port;
            _statusTip.SetToolTip(_status, tip);
            switch (status)
            {
                case JarStatus.Running: _status.ForeColor = t.Success; break;
                case JarStatus.Starting: _status.ForeColor = t.Accent; break;
                case JarStatus.Exited: _status.ForeColor = t.Warning; break;
                case JarStatus.Failed: _status.ForeColor = t.Danger; break;
                default: _status.ForeColor = t.SubText; break;
            }
            SetButtons(status == JarStatus.Running || status == JarStatus.Starting,
                       status == JarStatus.Stopped || status == JarStatus.Exited || status == JarStatus.Failed);
        }

        private void SetButtons(bool running, bool stopped)
        {
            _btnStart.Enabled = stopped;
            _btnStop.Enabled = running;
            _btnRestart.Enabled = !stopped;
        }

        private void OnLineAppended(string jarId, LogLine line)
        {
            // 只接收当前选中 jar 的日志；其它 jar 的日志在切换时从 LogStore 里补读
            if (string.IsNullOrEmpty(jarId)) return;
            if (!string.Equals(jarId, _jarId, StringComparison.OrdinalIgnoreCase)) return;
            lock (_pending)
            {
                _pending.Add(line);
            }
        }

        private void BeginInvokeSafe(Action act)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke(act);
                else act();
            }
            catch { }
        }

        private void Flush()
        {
            List<LogLine> batch;
            lock (_pending)
            {
                if (_pending.Count == 0) return;
                batch = new List<LogLine>(_pending);
                _pending.Clear();
            }

            string filter = (_filter.Box.Text ?? "").Trim();
            var show = new List<LogLine>();
            foreach (var line in batch)
            {
                if (filter.Length > 0 && (line.Text == null || line.Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                show.Add(line);
            }
            if (show.Count > 0) _view.AppendLines(show);
        }

        /// <summary>按当前过滤条件重建整个日志视图</summary>
        public void RebuildFromStore()
        {
            if (_store == null) return;
            var lines = _store.Get(_jarId);
            string filter = (_filter.Box.Text ?? "").Trim();
            var show = new List<LogLine>();
            foreach (var line in lines)
            {
                if (filter.Length > 0 && (line.Text == null || line.Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                show.Add(line);
            }
            _view.SetLines(show);
        }

        private void ClearBox()
        {
            _view.Clear();
            if (!string.IsNullOrEmpty(_jarId))
            {
                _store.Clear(_jarId);
                _store.AppendSys(_jarId, "日志已清空");
            }
        }

        private void CopyAll()
        {
            try
            {
                var text = _view.GetSelectedText();
                if (string.IsNullOrEmpty(text)) text = _view.GetAllText();
                if (text.Length > 0) Clipboard.SetText(text);
            }
            catch { }
        }

        private void ExportAll()
        {
            try
            {
                var item = CurrentItem;
                string name = item != null ? item.DisplayName : "log";
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                    dlg.FileName = name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log";
                    if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;

                    var sb = new StringBuilder();
                    var lines = _store.Get(_jarId);
                    foreach (var line in lines)
                        sb.AppendLine(line.TimeText + "  " + line.Text);
                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), "导出失败：" + ex.Message, "JarStart",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
