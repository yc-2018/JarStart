using System;
using System.Drawing;
using System.Windows.Forms;

namespace JarStart
{
    /// <summary>全局设置</summary>
    public class SettingsForm : Form
    {
        private AppConfig _cfg;
        private ComboBox _theme;
        private TextBox _accent, _java, _font;
        private NumericUpDown _maxLines, _fontSize;
        private CheckBox _autoScroll, _stopOnExit;
        private CheckBox _minimizeToTray;

        public SettingsForm(AppConfig cfg)
        {
            _cfg = cfg;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = Ui.Sz(470, 360);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = new Font("Microsoft YaHei UI", 9f);
            BackColor = Theme.Current.Bg;
            Text = "设置";

            BuildUi();
            LoadCfg();
        }

        private void BuildUi()
        {
            int y = Ui.Px(20);
            const int labelX = 18;
            const int boxX = 118;

            _theme = new ComboBox();
            _theme.DropDownStyle = ComboBoxStyle.DropDownList;
            _theme.Font = new Font("Microsoft YaHei UI", 9f);
            _theme.Items.Add("跟随系统");
            _theme.Items.Add("浅色");
            _theme.Items.Add("深色");
            _theme.SetBounds(Ui.Px(boxX), y - Ui.Px(3), Ui.Px(160), Ui.Px(24));
            AddLabel("主题", Ui.Px(labelX), y);
            Controls.Add(_theme);
            y += Ui.Px(34);

            _accent = NewBox();
            _accent.SetBounds(Ui.Px(boxX), y - Ui.Px(3), Ui.Px(160), Ui.Px(24));
            AddLabel("强调色", Ui.Px(labelX), y);
            Controls.Add(_accent);
            var accentTip = new Label();
            accentTip.Text = "形如 #4F8CFF，留空跟随系统";
            accentTip.ForeColor = Theme.Current.SubText;
            accentTip.Font = new Font("Microsoft YaHei UI", 8f);
            accentTip.AutoSize = false;
            accentTip.SetBounds(Ui.Px(boxX) + Ui.Px(170), y, Ui.Px(220), Ui.Px(24));
            accentTip.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(accentTip);
            y += Ui.Px(34);

            _java = NewBox();
            _java.SetBounds(Ui.Px(boxX), y - Ui.Px(3), Ui.Px(240), Ui.Px(24));
            AddLabel("默认 Java", Ui.Px(labelX), y);
            Controls.Add(_java);
            var browse = new FlatBtn();
            browse.Text = "浏览…";
            browse.Size = Ui.Sz(72, 26);
            browse.Font = new Font("Microsoft YaHei UI", 8.5f);
            browse.SetBounds(Ui.Px(boxX) + Ui.Px(246), y - Ui.Px(4), Ui.Px(72), Ui.Px(26));
            browse.Click += delegate { BrowseJava(); };
            Controls.Add(browse);
            y += Ui.Px(34);

            _maxLines = new NumericUpDown();
            _maxLines.Minimum = 200;
            _maxLines.Maximum = 100000;
            _maxLines.Increment = 500;
            _maxLines.Font = new Font("Microsoft YaHei UI", 9f);
            _maxLines.SetBounds(Ui.Px(boxX), y - Ui.Px(3), Ui.Px(120), Ui.Px(24));
            AddLabel("日志保留行数", Ui.Px(labelX), y);
            Controls.Add(_maxLines);
            y += Ui.Px(34);

            _font = NewBox();
            _font.SetBounds(Ui.Px(boxX), y - Ui.Px(3), Ui.Px(160), Ui.Px(24));
            AddLabel("日志字体", Ui.Px(labelX), y);
            Controls.Add(_font);

            _fontSize = new NumericUpDown();
            _fontSize.Minimum = 7;
            _fontSize.Maximum = 20;
            _fontSize.DecimalPlaces = 1;
            _fontSize.Increment = 0.5m;
            _fontSize.Font = new Font("Microsoft YaHei UI", 9f);
            _fontSize.SetBounds(Ui.Px(boxX) + Ui.Px(190), y - Ui.Px(3), Ui.Px(70), Ui.Px(24));
            Controls.Add(_fontSize);
            y += Ui.Px(34);

            _autoScroll = new CheckBox();
            _autoScroll.Text = "新日志自动滚动到底部";
            _autoScroll.FlatStyle = FlatStyle.Flat;
            _autoScroll.BackColor = Theme.Current.Bg;
            _autoScroll.ForeColor = Theme.Current.Text;
            _autoScroll.SetBounds(Ui.Px(boxX), y, Ui.Px(220), Ui.Px(24));
            Controls.Add(_autoScroll);
            y += Ui.Px(28);

            _stopOnExit = new CheckBox();
            _stopOnExit.Text = "退出时停止所有子进程（崩溃/被强杀也一起结束）";
            _stopOnExit.FlatStyle = FlatStyle.Flat;
            _stopOnExit.BackColor = Theme.Current.Bg;
            _stopOnExit.ForeColor = Theme.Current.Text;
            _stopOnExit.SetBounds(Ui.Px(boxX), y, Ui.Px(260), Ui.Px(24));
            Controls.Add(_stopOnExit);
            y += Ui.Px(28);

            _minimizeToTray = new CheckBox();
            _minimizeToTray.Text = "点关闭时缩到托盘（托盘菜单里可退出）";
            _minimizeToTray.FlatStyle = FlatStyle.Flat;
            _minimizeToTray.BackColor = Theme.Current.Bg;
            _minimizeToTray.ForeColor = Theme.Current.Text;
            _minimizeToTray.SetBounds(Ui.Px(boxX), y, Ui.Px(280), Ui.Px(24));
            Controls.Add(_minimizeToTray);
            y += Ui.Px(44);

            var ok = new FlatBtn();
            ok.Text = "保存";
            ok.Primary = true;
            ok.Size = Ui.Sz(84, 30);
            ok.SetBounds(Ui.Px(362), y, Ui.Px(84), Ui.Px(30));
            ok.Click += delegate { Save(); };
            Controls.Add(ok);

            var cancel = new FlatBtn();
            cancel.Text = "取消";
            cancel.Size = Ui.Sz(84, 30);
            cancel.SetBounds(Ui.Px(272), y, Ui.Px(84), Ui.Px(30));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.Handled = true; e.SuppressKeyPress = true; }
            };
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Enter) { Save(); return true; }
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessDialogKey(keyData);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ApplyDarkTitleBar(Handle, Theme.Current.Dark);
            Native.ApplyRoundCorners(Handle);
        }

        private void AddLabel(string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.ForeColor = Theme.Current.SubText;
            l.AutoSize = false;
            l.SetBounds(x, y, Ui.Px(96), Ui.Px(22));
            l.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(l);
        }

        private TextBox NewBox()
        {
            var t = new TextBox();
            t.Font = new Font("Microsoft YaHei UI", 9f);
            t.BorderStyle = BorderStyle.FixedSingle;
            return t;
        }

        private void BrowseJava()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择默认 JDK 目录";
                if (dlg.ShowDialog(this) == DialogResult.OK) _java.Text = dlg.SelectedPath;
            }
        }

        private void LoadCfg()
        {
            _theme.SelectedIndex = _cfg.Theme == "dark" ? 2 : (_cfg.Theme == "light" ? 1 : 0);
            _accent.Text = _cfg.Accent ?? "";
            _java.Text = _cfg.DefaultJavaPath ?? "";
            _maxLines.Value = Math.Max(_maxLines.Minimum, Math.Min(_maxLines.Maximum, _cfg.MaxLogLines));
            _font.Text = _cfg.LogFontFamily ?? "Cascadia Mono";
            _fontSize.Value = (decimal)Math.Max(7.0, Math.Min(20.0, _cfg.LogFontSize));
            _autoScroll.Checked = _cfg.AutoScroll;
            _stopOnExit.Checked = _cfg.StopAllOnExit;
            _minimizeToTray.Checked = _cfg.MinimizeToTray;
        }

        private void Save()
        {
            _cfg.Theme = _theme.SelectedIndex == 2 ? "dark" : (_theme.SelectedIndex == 1 ? "light" : "auto");
            var accent = (_accent.Text ?? "").Trim();
            if (accent.Length > 0 && !Theme.ParseColor(accent).HasValue)
            {
                MessageBox.Show(this, "强调色格式不对，示例：#4F8CFF", "JarStart",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _cfg.Accent = accent;
            _cfg.DefaultJavaPath = (_java.Text ?? "").Trim();
            _cfg.MaxLogLines = (int)_maxLines.Value;
            _cfg.LogFontFamily = (_font.Text ?? "").Trim();
            _cfg.LogFontSize = (float)_fontSize.Value;
            _cfg.AutoScroll = _autoScroll.Checked;
            _cfg.StopAllOnExit = _stopOnExit.Checked;
            _cfg.MinimizeToTray = _minimizeToTray.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
