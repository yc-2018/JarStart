using System;
using System.Drawing;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>主窗口：左侧 jar 分组树，右侧日志</summary>
    public partial class MainForm : Form
    {
        private AppConfig _cfg;
        private LogStore _log;
        private ProcessManager _pm;

        // 标题栏
        private Panel _bar;
        private Label _title;
        private IconBtn _btnSettings, _btnMin, _btnClose;

        // 左侧
        private Panel _leftBar;
        private IconBtn _btnAdd, _btnGroup, _btnStart, _btnStop, _btnRemove;
        private LineEdit _search;
        private JarTreePanel _tree;
        private Panel _splitter;

        // 右侧
        private LogPanel _logPanel;

        // 底部
        private Panel _foot;
        private Label _status;

        // 托盘
        private TrayIcon _tray;
        private bool _reallyExit;      // 从托盘菜单点"退出"时为真

        private int _leftWidth = Ui.Px(320);   // 左栏宽度（随 DPI 缩放）
        private bool _dragging;
        private int _dragStartX;
        private int _dragStartWidth;

        private readonly System.Windows.Forms.Timer _saveTimer;

        public MainForm(AppConfig cfg)
        {
            _cfg = cfg;
            _log = new LogStore(cfg.MaxLogLines);
            _pm = new ProcessManager(_log);
            _pm.KillChildrenOnExit = cfg.StopAllOnExit;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw, true);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            MinimumSize = Ui.Sz(880, 520);
            try { Size = Ui.Sz(Math.Max(880, cfg.WinW), Math.Max(520, cfg.WinH)); } catch { }
            DoubleBuffered = true;
            KeyPreview = true;
            Text = "JarStart 启动器";
            Icon = AppIcon.Get(Theme.Current.Dark);

            BuildUi();
            ApplyTheme();

            // 进程状态变化（启动/退出/识别到端口）时刷新左侧树和底部状态栏
            _pm.StatusChanged += delegate(string id)
            {
                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke(new Action(delegate { _tree.Invalidate(); UpdateStatus(); }));
                }
                catch { }
            };

            BuildTray();

            _saveTimer = new System.Windows.Forms.Timer();
            _saveTimer.Interval = 1500;
            _saveTimer.Tick += delegate { _saveTimer.Stop(); _cfg.Save(); };
        }

        private void BuildUi()
        {
            SuspendLayout();

            // ——— 标题栏 ———
            _bar = new Panel();
            _bar.BackColor = Theme.Current.Bg;
            _bar.Height = 44;
            _bar.MouseDown += Bar_MouseDown;
            _bar.DoubleClick += delegate
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;
            };
            Controls.Add(_bar);

            _title = new Label();
            _title.Text = "JarStart 启动器";
            _title.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            _title.AutoSize = false;
            _title.ForeColor = Theme.Current.Text;
            _title.BackColor = Color.Transparent;
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _bar.Controls.Add(_title);

            _btnSettings = new IconBtn();
            _btnSettings.Kind = "gear";
            _btnSettings.SetTip("设置");
            _btnSettings.Click += delegate { OpenSettings(); };
            _bar.Controls.Add(_btnSettings);

            _btnMin = new IconBtn();
            _btnMin.Kind = "min";
            _btnMin.SetTip("最小化");
            _btnMin.Click += delegate { WindowState = FormWindowState.Minimized; };
            _bar.Controls.Add(_btnMin);

            _btnClose = new IconBtn();
            _btnClose.Kind = "close";
            _btnClose.SetTip("关闭");
            _btnClose.Click += delegate { Close(); };
            _bar.Controls.Add(_btnClose);

            // ——— 左侧工具条 ———
            _leftBar = new Panel();
            _leftBar.BackColor = Theme.Current.Bg;
            _leftBar.Height = 40;
            _leftBar.AllowDrop = true;
            _leftBar.DragEnter += LeftBar_DragEnter;
            _leftBar.DragDrop += LeftBar_DragDrop;
            Controls.Add(_leftBar);

            _btnAdd = new IconBtn();
            _btnAdd.Kind = "plus";
            _btnAdd.SetTip("添加 jar");
            _btnAdd.Click += delegate { AddJars(); };
            _leftBar.Controls.Add(_btnAdd);

            _btnGroup = new IconBtn();
            _btnGroup.Kind = "group";
            _btnGroup.SetTip("新建分组");
            _btnGroup.Click += delegate { NewGroup(); };
            _leftBar.Controls.Add(_btnGroup);

            _btnStart = new IconBtn();
            _btnStart.Kind = "play";
            _btnStart.SetTip("启动选中的 jar");
            _btnStart.Click += delegate { StartSelected(); };
            _leftBar.Controls.Add(_btnStart);

            _btnStop = new IconBtn();
            _btnStop.Kind = "stop";
            _btnStop.SetTip("停止选中的 jar");
            _btnStop.Click += delegate { StopSelected(); };
            _leftBar.Controls.Add(_btnStop);

            _btnRemove = new IconBtn();
            _btnRemove.Kind = "trash";
            _btnRemove.SetTip("从列表移除");
            _btnRemove.Click += delegate { RemoveSelected(); };
            _leftBar.Controls.Add(_btnRemove);

            _search = new LineEdit();
            _search.Height = 30;
            _search.Placeholder = "搜索 jar 名称或路径…";
            _search.Box.TextChanged += delegate
            {
                _tree.Filter = _search.Box.Text;
            };
            Controls.Add(_search);

            // ——— 左侧树 ———
            _tree = new JarTreePanel(_cfg, _pm);
            _tree.SelectionChanged += delegate(string id)
            {
                _logPanel.SetJar(id);
                _cfg.LastSelectedId = id;
                ScheduleSave();
                UpdateStatus();
            };
            _tree.ItemActivated += delegate(string id) { ToggleJar(id); };
            _tree.ContextRequested += delegate(TreeHit hit) { ShowContextMenu(hit); };
            Controls.Add(_tree);

            // ——— 分隔条 ———
            _splitter = new Panel();
            _splitter.BackColor = Theme.Current.Bg;
            _splitter.Width = 5;
            _splitter.Cursor = Cursors.SizeWE;
            _splitter.MouseDown += Splitter_MouseDown;
            _splitter.MouseMove += Splitter_MouseMove;
            _splitter.MouseUp += Splitter_MouseUp;
            Controls.Add(_splitter);

            // ——— 右侧日志 ———
            _logPanel = new LogPanel(_cfg, _log, _pm);
            _logPanel.StartRequested += delegate { StartSelected(); };
            _logPanel.StopRequested += delegate { StopSelected(); };
            _logPanel.RestartRequested += delegate { RestartSelected(); };
            Controls.Add(_logPanel);

            // ——— 底部状态栏 ———
            _foot = new Panel();
            _foot.Height = 26;
            _foot.BackColor = Theme.Current.Bg;
            Controls.Add(_foot);

            _status = new Label();
            _status.Font = new Font("Microsoft YaHei UI", 8.5f);
            _status.ForeColor = Theme.Current.SubText;
            _status.AutoSize = false;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _foot.Controls.Add(_status);

            Relayout();
            ResumeLayout(true);
        }
    }
}
