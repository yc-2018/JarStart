using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>编辑单个 jar 的配置</summary>
    public class JarEditForm : Form
    {
        private AppConfig _cfg;
        private JarItem _item;
        private bool _isNew;

        private TextBox _name, _jarPath, _javaPath, _jvmArgs, _programArgs, _workDir, _envVars, _port;
        private ComboBox _group, _encoding;
        private CheckBox _autoStart;
        private Label _javaInfo;
        private ToolTip _javaTip;

        public JarItem Result { get { return _item; } }

        public JarEditForm(AppConfig cfg, JarItem item, bool isNew)
        {
            _cfg = cfg;
            _item = item.Clone();
            _isNew = isNew;

            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = Ui.Sz(560, 470);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Theme.Current.Bg;
            Text = isNew ? "添加 jar" : "编辑 jar 配置";

            BuildUi();
            LoadItem();
        }

        private void BuildUi()
        {
            int labelX = Ui.Px(18);
            int boxX = Ui.Px(110);
            int boxW = Ui.Px(430);
            int y = Ui.Px(18);
            const int rowH = 32;

            _name = NewBox();
            AddLabel("显示名", labelX, y);
            _name.SetBounds(boxX, y - Ui.Px(3), boxW - Ui.Px(110), Ui.Px(24));
            Controls.Add(_name);
            y += Ui.Px(rowH);

            _jarPath = NewBox();
            AddLabel("jar 路径", labelX, y);
            _jarPath.SetBounds(boxX, y - Ui.Px(3), boxW - Ui.Px(110), Ui.Px(24));
            Controls.Add(_jarPath);
            var browseJar = NewSmallBtn("浏览…", delegate { BrowseFile(); });
            browseJar.SetBounds(boxX + boxW - Ui.Px(106), y - Ui.Px(4), Ui.Px(106), Ui.Px(26));
            Controls.Add(browseJar);
            y += Ui.Px(rowH);

            _group = new ComboBox();
            _group.DropDownStyle = ComboBoxStyle.DropDownList;
            _group.Font = new Font("Microsoft YaHei UI", 9f);
            _group.SetBounds(boxX, y - Ui.Px(3), boxW - Ui.Px(110), Ui.Px(24));
            AddLabel("所属分组", labelX, y);
            Controls.Add(_group);
            var newGroup = NewSmallBtn("新建分组…", delegate { CreateGroup(); });
            newGroup.SetBounds(boxX + boxW - Ui.Px(106), y - Ui.Px(4), Ui.Px(106), Ui.Px(26));
            Controls.Add(newGroup);
            y += Ui.Px(rowH);

            _javaPath = NewBox();
            AddLabel("Java 环境", labelX, y);
            _javaPath.SetBounds(boxX, y - Ui.Px(3), boxW - Ui.Px(210), Ui.Px(24));
            Controls.Add(_javaPath);
            _javaPath.TextChanged += delegate
            {
                // 路径改了，之前的版本信息就不作数了
                if (_javaInfo == null) return;
                _javaInfo.ForeColor = Theme.Current.SubText;
                _javaInfo.Text = "点击「检测」查看该 java 的版本";
            };
            var browseJava = NewSmallBtn("浏览…", delegate { BrowseJava(); });
            browseJava.SetBounds(boxX + boxW - Ui.Px(200), y - Ui.Px(4), Ui.Px(62), Ui.Px(26));
            Controls.Add(browseJava);
            var probe = NewSmallBtn("检测", delegate { ProbeJava(); });
            probe.SetBounds(boxX + boxW - Ui.Px(134), y - Ui.Px(4), Ui.Px(62), Ui.Px(26));
            Controls.Add(probe);
            y += Ui.Px(rowH);

            // 版本信息单独占一行，右边那点空位根本放不下版本号
            _javaInfo = new Label();
            _javaInfo.Font = new Font("Microsoft YaHei UI", 8.5f);
            _javaInfo.ForeColor = Theme.Current.SubText;
            _javaInfo.AutoSize = false;
            _javaInfo.SetBounds(boxX, y - Ui.Px(2), boxW, Ui.Px(20));
            _javaInfo.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(_javaInfo);
            _javaTip = new ToolTip();
            y += Ui.Px(24);

            _jvmArgs = NewBox();
            AddLabel("JVM 参数", labelX, y);
            _jvmArgs.SetBounds(boxX, y - Ui.Px(3), boxW, Ui.Px(24));
            Controls.Add(_jvmArgs);
            y += Ui.Px(rowH);

            _programArgs = NewBox();
            AddLabel("程序参数", labelX, y);
            _programArgs.SetBounds(boxX, y - Ui.Px(3), boxW, Ui.Px(24));
            Controls.Add(_programArgs);
            y += Ui.Px(rowH);

            _workDir = NewBox();
            AddLabel("工作目录", labelX, y);
            _workDir.SetBounds(boxX, y - Ui.Px(3), boxW - Ui.Px(110), Ui.Px(24));
            Controls.Add(_workDir);
            var browseDir = NewSmallBtn("浏览…", delegate { BrowseDir(); });
            browseDir.SetBounds(boxX + boxW - Ui.Px(106), y - Ui.Px(4), Ui.Px(106), Ui.Px(26));
            Controls.Add(browseDir);
            y += Ui.Px(rowH);

            _envVars = NewBox();
            _envVars.Multiline = true;
            _envVars.ScrollBars = ScrollBars.Vertical;
            _envVars.AcceptsReturn = true;
            AddLabel("环境变量", labelX, y);
            _envVars.SetBounds(boxX, y - Ui.Px(3), boxW, Ui.Px(48));
            Controls.Add(_envVars);
            y += Ui.Px(54);

            _encoding = new ComboBox();
            _encoding.DropDownStyle = ComboBoxStyle.DropDownList;
            _encoding.Font = new Font("Microsoft YaHei UI", 9f);
            _encoding.Items.Add("UTF-8");
            _encoding.Items.Add("GBK");
            _encoding.SetBounds(boxX, y - Ui.Px(3), Ui.Px(100), Ui.Px(24));
            AddLabel("日志编码", labelX, y);
            Controls.Add(_encoding);

            _port = NewBox();
            _port.SetBounds(boxX + Ui.Px(166), y - Ui.Px(3), Ui.Px(90), Ui.Px(24));
            AddLabel("端口", boxX + Ui.Px(112), y, Ui.Px(50));
            Controls.Add(_port);

            _autoStart = new CheckBox();
            _autoStart.Text = "随启动器自动启动";
            _autoStart.FlatStyle = FlatStyle.Flat;
            _autoStart.BackColor = Theme.Current.Bg;
            _autoStart.ForeColor = Theme.Current.Text;
            _autoStart.SetBounds(boxX + Ui.Px(272), y - Ui.Px(2), Ui.Px(170), Ui.Px(24));
            Controls.Add(_autoStart);
            y += Ui.Px(40);

            var ok = new FlatBtn();
            ok.Text = "保存";
            ok.Primary = true;
            ok.Size = Ui.Sz(84, 30);
            ok.SetBounds(Ui.Px(452), y, Ui.Px(84), Ui.Px(30));
            ok.Click += delegate { Save(); };
            Controls.Add(ok);

            var cancel = new FlatBtn();
            cancel.Text = "取消";
            cancel.Size = Ui.Sz(84, 30);
            cancel.SetBounds(Ui.Px(362), y, Ui.Px(84), Ui.Px(30));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.Handled = true; e.SuppressKeyPress = true; }
            };
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            // 环境变量是多行输入框，回车要用来换行，其它地方回车即保存
            if (keyData == Keys.Enter)
            {
                if (ActiveControl != _envVars) { Save(); return true; }
                return base.ProcessDialogKey(keyData);
            }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ApplyDarkTitleBar(Handle, Theme.Current.Dark);
            Native.ApplyRoundCorners(Handle);
        }

        private Label AddLabel(string text, int x, int y)
        {
            return AddLabel(text, x, y, Ui.Px(88));
        }

        private Label AddLabel(string text, int x, int y, int width)
        {
            var l = new Label();
            l.Text = text;
            l.ForeColor = Theme.Current.SubText;
            l.AutoSize = false;
            l.SetBounds(x, y, width, Ui.Px(22));
            l.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(l);
            return l;
        }

        private TextBox NewBox()
        {
            var t = new TextBox();
            t.Font = new Font("Microsoft YaHei UI", 9f);
            t.BorderStyle = BorderStyle.FixedSingle;
            return t;
        }

        private FlatBtn NewSmallBtn(string text, EventHandler click)
        {
            var b = new FlatBtn();
            b.Text = text;
            b.Size = Ui.Sz(80, 26);
            b.Font = new Font("Microsoft YaHei UI", 8.5f);
            b.Click += click;
            return b;
        }

        private void LoadItem()
        {
            _name.Text = _item.Name;
            _jarPath.Text = _item.JarPath;
            _javaPath.Text = _item.JavaPath;
            _jvmArgs.Text = _item.JvmArgs;
            _programArgs.Text = _item.ProgramArgs;
            _workDir.Text = _item.WorkDir;
            _envVars.Text = _item.EnvVars;
            _port.Text = _item.Port;
            _autoStart.Checked = _item.AutoStart;
            _encoding.SelectedItem = string.IsNullOrEmpty(_item.Encoding) ? "UTF-8" : _item.Encoding;
            if (_encoding.SelectedIndex < 0) _encoding.SelectedIndex = 0;

            _group.Items.Add(JarTreePanel.UngroupedGroup);
            foreach (var g in _cfg.Groups) _group.Items.Add(g);
            int idx = _group.Items.IndexOf(string.IsNullOrEmpty(_item.Group) ? JarTreePanel.UngroupedGroup : _item.Group);
            _group.SelectedIndex = idx >= 0 ? idx : 0;

            // 打开就顺手检测一次，用户能立刻看到用的是哪个版本的 java
            if (!string.IsNullOrWhiteSpace(_item.JavaPath)) ProbeJava();
            else _javaInfo.Text = "留空则使用系统环境变量里的 java（JAVA_HOME → PATH）";
        }

        private void BrowseFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择 jar 包";
                dlg.Filter = "jar 文件 (*.jar)|*.jar|所有文件 (*.*)|*.*";
                dlg.Multiselect = false;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _jarPath.Text = dlg.FileName;
                    if (string.IsNullOrWhiteSpace(_name.Text))
                        _name.Text = Path.GetFileNameWithoutExtension(dlg.FileName);
                }
            }
        }

        private void BrowseJava()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择 JDK 目录（也可以直接填写 java.exe 的完整路径）";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _javaPath.Text = dlg.SelectedPath;
                    ProbeJava();
                }
            }
        }

        private void BrowseDir()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择工作目录";
                if (dlg.ShowDialog(this) == DialogResult.OK) _workDir.Text = dlg.SelectedPath;
            }
        }

        private void ProbeJava()
        {
            string path = (_javaPath.Text ?? "").Trim();
            if (path.Length == 0)
            {
                // 没填就用系统环境变量里的 java 试一下，好让用户看到实际用的是哪个版本
                path = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (string.IsNullOrWhiteSpace(path)) path = "java";
            }

            string text = JdkResolver.ProbeVersion(path);
            bool bad = text.StartsWith("检测失败") || text.StartsWith("路径无效");
            if (bad)
            {
                _javaInfo.Text = "✖ " + text;
                _javaInfo.ForeColor = Theme.Current.Danger;
                if (_javaTip != null) _javaTip.SetToolTip(_javaInfo, text);
            }
            else
            {
                string summary = JdkResolver.SummarizeVersion(text);
                string where = string.IsNullOrWhiteSpace(_javaPath.Text) ? "（系统环境变量）" : "";
                _javaInfo.Text = "✔ " + summary + where;
                _javaInfo.ForeColor = Theme.Current.Success;
                if (_javaTip != null) _javaTip.SetToolTip(_javaInfo, text);
            }
        }

        private void CreateGroup()
        {
            using (var dlg = new TextPromptForm("新建分组", "分组名称", ""))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string name = dlg.Value;
                if (string.IsNullOrWhiteSpace(name)) return;
                if (!_cfg.Groups.Contains(name)) _cfg.Groups.Add(name);
                _group.Items.Clear();
                _group.Items.Add(JarTreePanel.UngroupedGroup);
                foreach (var g in _cfg.Groups) _group.Items.Add(g);
                _group.SelectedItem = name;
            }
        }

        private void Save()
        {
            var jar = (_jarPath.Text ?? "").Trim();
            if (jar.Length == 0)
            {
                MessageBox.Show(this, "请填写 jar 路径", "JarStart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!File.Exists(jar))
            {
                MessageBox.Show(this, "找不到这个文件：\n" + jar, "JarStart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _item.Name = (_name.Text ?? "").Trim();
            _item.JarPath = jar;
            var grp = _group.SelectedItem as string;
            _item.Group = (grp == null || grp == JarTreePanel.UngroupedGroup) ? "" : grp;
            _item.JavaPath = (_javaPath.Text ?? "").Trim();
            _item.JvmArgs = (_jvmArgs.Text ?? "").Trim();
            _item.ProgramArgs = (_programArgs.Text ?? "").Trim();
            _item.WorkDir = (_workDir.Text ?? "").Trim();
            _item.EnvVars = (_envVars.Text ?? "").Trim();
            _item.Port = (_port.Text ?? "").Trim();
            _item.Encoding = _encoding.SelectedItem as string;
            if (string.IsNullOrEmpty(_item.Encoding)) _item.Encoding = "UTF-8";
            _item.AutoStart = _autoStart.Checked;

            if (string.IsNullOrEmpty(_item.Id)) _item.Id = Guid.NewGuid().ToString("N");
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
