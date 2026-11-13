using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace JarStart
{
    public partial class MainForm
    {
        private void ScheduleSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private JarItem FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var it in _cfg.Items)
                if (string.Equals(it.Id, id, StringComparison.OrdinalIgnoreCase)) return it;
            return null;
        }

        private void UpdateStatus()
        {
            int running = 0;
            foreach (var it in _cfg.Items)
                if (_pm.IsRunning(it.Id)) running++;
            string text = "共 " + _cfg.Items.Count + " 个 jar · 运行中 " + running + " 个";
            var sel = FindById(_tree.SelectedId);
            if (sel != null)
            {
                string jdk = string.IsNullOrWhiteSpace(sel.JavaPath)
                    ? (string.IsNullOrWhiteSpace(_cfg.DefaultJavaPath) ? "系统环境" : _cfg.DefaultJavaPath)
                    : sel.JavaPath;
                text += "  ·  " + sel.DisplayName + "  ·  Java：" + jdk;
                int port = _tree.PortOf(sel);
                if (port > 0) text += "  ·  端口：" + port;
            }
            _status.Text = text;
        }

        // ————————————————— 添加 —————————————————

        private void AddJars()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择要管理的 jar 包（可多选）";
                dlg.Filter = "jar 文件 (*.jar)|*.jar|所有文件 (*.*)|*.*";
                dlg.Multiselect = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                AddJarPaths(dlg.FileNames);
            }
        }

        private void AddJarPaths(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            int added = 0;
            string firstId = null;
            foreach (var path in paths)
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    bool dup = false;
                    foreach (var it in _cfg.Items)
                        if (string.Equals(it.JarPath, path, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                    if (dup) continue;

                    var item = new JarItem();
                    item.Id = Guid.NewGuid().ToString("N");
                    item.JarPath = path;
                    item.Name = Path.GetFileNameWithoutExtension(path);
                    item.Group = "";
                    item.Encoding = "UTF-8";
                    _cfg.Items.Add(item);
                    added++;
                    if (firstId == null) firstId = item.Id;
                }
                catch { }
            }
            if (added > 0)
            {
                _tree.Rebuild();
                if (firstId != null) _tree.SelectedId = firstId;
                ScheduleSave();
                UpdateStatus();
            }
            else
            {
                MessageBox.Show(this, "没有新增 jar（可能已经添加过了）", "JarStart",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ————————————————— 分组 —————————————————

        private void NewGroup()
        {
            using (var dlg = new TextPromptForm("新建分组", "分组名称", ""))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string name = dlg.Value;
                if (string.IsNullOrWhiteSpace(name)) return;
                if (_cfg.Groups.Contains(name))
                {
                    MessageBox.Show(this, "已经有同名分组了", "JarStart", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                _cfg.Groups.Add(name);
                _tree.Rebuild();
                ScheduleSave();
            }
        }

        private void RenameGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return;
            if (group == JarTreePanel.AllGroup || group == JarTreePanel.UngroupedGroup) return;
            using (var dlg = new TextPromptForm("重命名分组", "新的分组名称", group))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string name = dlg.Value;
                if (string.IsNullOrWhiteSpace(name) || name == group) return;
                if (_cfg.Groups.Contains(name))
                {
                    MessageBox.Show(this, "已经有同名分组了", "JarStart", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int idx = _cfg.Groups.IndexOf(group);
                if (idx >= 0) _cfg.Groups[idx] = name;
                foreach (var it in _cfg.Items)
                    if (string.Equals(it.Group, group, StringComparison.OrdinalIgnoreCase)) it.Group = name;
                _tree.Rebuild();
                ScheduleSave();
            }
        }

        private void DeleteGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return;
            if (group == JarTreePanel.AllGroup || group == JarTreePanel.UngroupedGroup) return;
            var r = MessageBox.Show(this, "删除分组“" + group + "”？\n组内的 jar 会移到未分组。",
                "JarStart", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (r != DialogResult.OK) return;
            _cfg.Groups.Remove(group);
            foreach (var it in _cfg.Items)
                if (string.Equals(it.Group, group, StringComparison.OrdinalIgnoreCase)) it.Group = "";
            _tree.Rebuild();
            ScheduleSave();
        }

        private void MoveToGroup(string jarId, string group)
        {
            var item = FindById(jarId);
            if (item == null) return;
            item.Group = (group == null || group == JarTreePanel.UngroupedGroup) ? "" : group;
            _tree.Rebuild();
            ScheduleSave();
            UpdateStatus();
        }

        // ————————————————— 增删改 —————————————————

        private void EditJar(string jarId)
        {
            var item = FindById(jarId);
            if (item == null) return;
            using (var dlg = new JarEditForm(_cfg, item, false))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var edited = dlg.Result;
                for (int i = 0; i < _cfg.Items.Count; i++)
                {
                    if (string.Equals(_cfg.Items[i].Id, jarId, StringComparison.OrdinalIgnoreCase))
                    {
                        _cfg.Items[i] = edited;
                        break;
                    }
                }
                _tree.Rebuild();
                _tree.SelectedId = jarId;
                ScheduleSave();
                UpdateStatus();
            }
        }

        private void RemoveSelected()
        {
            RemoveJar(_tree.SelectedId);
        }

        private void RemoveJar(string jarId)
        {
            var item = FindById(jarId);
            if (item == null) return;
            if (_pm.IsRunning(jarId))
            {
                var r = MessageBox.Show(this, "“" + item.DisplayName + "”还在运行，先停止再移除？",
                    "JarStart", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (r != DialogResult.OK) return;
                _pm.Stop(jarId, true);
            }
            _cfg.Items.RemoveAll(delegate(JarItem x) { return string.Equals(x.Id, jarId, StringComparison.OrdinalIgnoreCase); });
            _log.Clear(jarId);
            _tree.Rebuild();
            if (string.Equals(_tree.SelectedId, jarId, StringComparison.OrdinalIgnoreCase))
            {
                _tree.SelectedId = "";
                _logPanel.SetJar("");
            }
            ScheduleSave();
            UpdateStatus();
        }

        // ————————————————— 启停 —————————————————

        private void StartJar(string jarId)
        {
            var item = FindById(jarId);
            if (item == null) return;
            string error;
            if (!_pm.Start(item, _cfg.DefaultJavaPath, out error))
            {
                _logPanel.SetJar(jarId);
                _tree.Rebuild();
                MessageBox.Show(this, error, "JarStart", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                _logPanel.SetJar(jarId);
            }
            _tree.Invalidate();
            UpdateStatus();
        }

        private void StopJar(string jarId)
        {
            if (!_pm.IsRunning(jarId)) return;
            _pm.Stop(jarId, false);
            _tree.Invalidate();
            UpdateStatus();
        }

        private void RestartJar(string jarId)
        {
            var item = FindById(jarId);
            if (item == null) return;
            if (_pm.IsRunning(jarId)) _pm.Stop(jarId, true);
            System.Threading.Thread.Sleep(200);
            StartJar(jarId);
        }

        private void ToggleJar(string jarId)
        {
            if (_pm.IsRunning(jarId)) StopJar(jarId);
            else StartJar(jarId);
        }

        private void StartSelected()
        {
            var item = FindById(_tree.SelectedId);
            if (item == null)
            {
                MessageBox.Show(this, "请先在左侧选择一个 jar", "JarStart", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            StartJar(item.Id);
        }

        private void StopSelected()
        {
            var item = FindById(_tree.SelectedId);
            if (item == null) return;
            StopJar(item.Id);
        }

        private void RestartSelected()
        {
            var item = FindById(_tree.SelectedId);
            if (item == null) return;
            RestartJar(item.Id);
        }

        private void StartAll()
        {
            foreach (var it in new List<JarItem>(_cfg.Items))
            {
                if (_pm.IsRunning(it.Id)) continue;
                string error;
                _pm.Start(it, _cfg.DefaultJavaPath, out error);
            }
            _tree.Rebuild();
            UpdateStatus();
        }

        private void StopAll()
        {
            _pm.StopAll(true);
            _tree.Invalidate();
            UpdateStatus();
        }

        // ————————————————— 右键菜单 —————————————————

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Renderer = new MenuRenderer();
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.ShowImageMargin = false;
            return menu;
        }

        private void ShowContextMenu(TreeHit hit)
        {
            if (hit == null) return;
            var menu = BuildMenu();

            if (hit.IsGroup)
            {
                string group = hit.GroupName;
                bool special = group == JarTreePanel.AllGroup || group == JarTreePanel.UngroupedGroup;

                if (!special)
                {
                    var miRename = new ToolStripMenuItem("重命名分组…");
                    miRename.Click += delegate { RenameGroup(group); };
                    menu.Items.Add(miRename);

                    var miDelete = new ToolStripMenuItem("删除分组");
                    miDelete.Click += delegate { DeleteGroup(group); };
                    menu.Items.Add(miDelete);
                    menu.Items.Add(new ToolStripSeparator());
                }
                else
                {
                    var miNew = new ToolStripMenuItem("新建分组…");
                    miNew.Click += delegate { NewGroup(); };
                    menu.Items.Add(miNew);
                    menu.Items.Add(new ToolStripSeparator());
                }

                var miGroupStart = new ToolStripMenuItem("启动本组");
                miGroupStart.Click += delegate { StartGroup(group); };
                menu.Items.Add(miGroupStart);

                var miGroupStop = new ToolStripMenuItem("停止本组");
                miGroupStop.Click += delegate { StopGroup(group); };
                menu.Items.Add(miGroupStop);

                menu.Show(Cursor.Position);
                return;
            }

            var item = FindById(hit.JarId);
            if (item == null) return;

            bool running = _pm.IsRunning(item.Id);

            var miStart = new ToolStripMenuItem("启动");
            miStart.Enabled = !running;
            miStart.Click += delegate { StartJar(item.Id); };
            menu.Items.Add(miStart);

            var miStop = new ToolStripMenuItem("停止");
            miStop.Enabled = running;
            miStop.Click += delegate { StopJar(item.Id); };
            menu.Items.Add(miStop);

            var miRestart = new ToolStripMenuItem("重启");
            miRestart.Click += delegate { RestartJar(item.Id); };
            menu.Items.Add(miRestart);

            var miKill = new ToolStripMenuItem("强制结束进程树");
            miKill.Enabled = running;
            miKill.Click += delegate { _pm.Stop(item.Id, true); _tree.Invalidate(); };
            menu.Items.Add(miKill);

            menu.Items.Add(new ToolStripSeparator());

            var miEdit = new ToolStripMenuItem("编辑配置…");
            miEdit.Click += delegate { EditJar(item.Id); };
            menu.Items.Add(miEdit);

            var groupMenu = new ToolStripMenuItem("移动到分组");
            var miUngrouped = new ToolStripMenuItem(JarTreePanel.UngroupedGroup);
            miUngrouped.Click += delegate { MoveToGroup(item.Id, JarTreePanel.UngroupedGroup); };
            groupMenu.DropDownItems.Add(miUngrouped);
            foreach (var g in _cfg.Groups)
            {
                var gg = g;
                var miG = new ToolStripMenuItem(gg);
                miG.Click += delegate { MoveToGroup(item.Id, gg); };
                groupMenu.DropDownItems.Add(miG);
            }
            groupMenu.DropDownItems.Add(new ToolStripSeparator());
            var miNewGroup = new ToolStripMenuItem("新建分组…");
            miNewGroup.Click += delegate { NewGroup(); };
            groupMenu.DropDownItems.Add(miNewGroup);
            menu.Items.Add(groupMenu);

            var miCopy = new ToolStripMenuItem("复制完整路径");
            miCopy.Click += delegate { try { Clipboard.SetText(item.JarPath); } catch { } };
            menu.Items.Add(miCopy);

            var miFolder = new ToolStripMenuItem("在资源管理器中打开");
            miFolder.Click += delegate { OpenInExplorer(item.JarPath); };
            menu.Items.Add(miFolder);

            menu.Items.Add(new ToolStripSeparator());

            var miRemove = new ToolStripMenuItem("从列表移除");
            miRemove.Click += delegate { RemoveJar(item.Id); };
            menu.Items.Add(miRemove);

            menu.Show(Cursor.Position);
        }

        private void StartGroup(string group)
        {
            foreach (var it in new List<JarItem>(_cfg.Items))
            {
                bool match = group == JarTreePanel.AllGroup
                    ? true
                    : group == JarTreePanel.UngroupedGroup
                        ? string.IsNullOrEmpty(it.Group)
                        : string.Equals(it.Group, group, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
                if (_pm.IsRunning(it.Id)) continue;
                string error;
                _pm.Start(it, _cfg.DefaultJavaPath, out error);
            }
            _tree.Invalidate();
            UpdateStatus();
        }

        private void StopGroup(string group)
        {
            foreach (var it in new List<JarItem>(_cfg.Items))
            {
                bool match = group == JarTreePanel.AllGroup
                    ? true
                    : group == JarTreePanel.UngroupedGroup
                        ? string.IsNullOrEmpty(it.Group)
                        : string.Equals(it.Group, group, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
                _pm.Stop(it.Id, false);
            }
            _tree.Invalidate();
            UpdateStatus();
        }

        private static void OpenInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else if (Directory.Exists(path))
                    Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch { }
        }

        // ————————————————— 设置与拖放 —————————————————

        private void OpenSettings()
        {
            using (var dlg = new SettingsForm(_cfg))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Theme.Apply(_cfg);
                _log.SetMax(_cfg.MaxLogLines);
                _pm.KillChildrenOnExit = _cfg.StopAllOnExit;
                ApplyTheme();
                ScheduleSave();
            }
        }

        private void LeftBar_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void LeftBar_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths == null) return;
                var jars = new List<string>();
                foreach (var p in paths)
                {
                    try
                    {
                        if (Directory.Exists(p))
                        {
                            foreach (var f in Directory.GetFiles(p, "*.jar"))
                                jars.Add(f);
                        }
                        else if (File.Exists(p)) jars.Add(p);
                    }
                    catch { }
                }
                AddJarPaths(jars.ToArray());
            }
            catch { }
        }

        // ————————————————— 托盘 —————————————————

        private void BuildTray()
        {
            // 自动化测试时可以不往系统托盘里放图标，免得在用户任务栏上留痕迹
            bool showIcon = Environment.GetEnvironmentVariable("JARSTART_NO_TRAY") != "1";
            _tray = new TrayIcon(showIcon);
            _tray.ShowRequested += delegate { RestoreFromTray(); };
            _tray.ExitRequested += delegate
            {
                _reallyExit = true;
                Close();
            };
            _tray.SettingsRequested += delegate { RestoreFromTray(); OpenSettings(); };
            _tray.StartAllRequested += delegate { StartAll(); };
            _tray.StopAllRequested += delegate { StopAll(); };
        }

        private void HideToTray()
        {
            try
            {
                Visible = false;
                if (!_cfg.TrayHintShown)
                {
                    _cfg.TrayHintShown = true;
                    _tray.Balloon("已缩到托盘",
                        "双击托盘图标可以恢复窗口；右键托盘图标可以退出。\n（可在「设置」里关掉这个行为）");
                    ScheduleSave();
                }
            }
            catch { }
        }

        private void RestoreFromTray()
        {
            try
            {
                if (!Visible) Visible = true;
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Native.ForceForeground(this);
                Activate();
            }
            catch { }
        }

        // ————————————————— 生命周期 —————————————————

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                // WinX/WinY 为 -1 表示"还没记过位置"；负数是允许的（自动化测试会放到屏幕外）
                if (_cfg.WinX != -1 && _cfg.WinY != -1)
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = new Point(_cfg.WinX, _cfg.WinY);
                    Size = Ui.Sz(Math.Max(880, _cfg.WinW), Math.Max(520, _cfg.WinH));
                }
            }
            catch { }
            EnsureOnScreen();
            Relayout();

            _tree.Rebuild();
            if (!string.IsNullOrEmpty(_cfg.LastSelectedId) && FindById(_cfg.LastSelectedId) != null)
            {
                _tree.SelectedId = _cfg.LastSelectedId;
                _tree.EnsureVisible(_cfg.LastSelectedId);
            }
            UpdateStatus();

            // 自动化测试用：设了环境变量就反复把托盘菜单弹出来（弹在窗口所在处，测试时窗口在屏幕外）
            if (Environment.GetEnvironmentVariable("JARSTART_SHOW_TRAY_MENU") == "1" && _tray != null)
            {
                var menuTimer = new System.Windows.Forms.Timer();
                menuTimer.Interval = 1200;
                menuTimer.Tick += delegate
                {
                    try { _tray.ShowMenuAt(new Point(Left + 20, Top + 20)); } catch { }
                };
                menuTimer.Start();
            }

            // 延迟一点再自动启动，避免和界面初始化抢资源
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 700;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                bool any = false;
                foreach (var it in new List<JarItem>(_cfg.Items))
                {
                    if (!it.AutoStart) continue;
                    any = true;
                    string error;
                    _pm.Start(it, _cfg.DefaultJavaPath, out error);
                }
                if (any) { _tree.Invalidate(); UpdateStatus(); }
            };
            timer.Start();
        }

        private void EnsureOnScreen()
        {
            // 自动化测试时窗口故意放在屏幕外，别把它拉回来
            if (Environment.GetEnvironmentVariable("JARSTART_NO_REPOSITION") == "1") return;
            try
            {
                var area = Screen.FromControl(this).WorkingArea;
                if (Left < area.Left - 50 || Top < area.Top - 50 ||
                    Left > area.Right - 100 || Top > area.Bottom - 80)
                {
                    StartPosition = FormStartPosition.CenterScreen;
                    Location = new Point(area.Left + 80, area.Top + 60);
                }
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 点关闭只是缩到托盘，不是真退出
            if (!_reallyExit && _cfg.MinimizeToTray)
            {
                e.Cancel = true;
                HideToTray();
                base.OnFormClosing(e);
                return;
            }

            try
            {
                _cfg.WinX = Left;
                _cfg.WinY = Top;
                // 窗口尺寸按逻辑像素保存，换一台缩放比例不同的机器也不会越存越大
                _cfg.WinW = (int)(Width / Ui.Scale);
                _cfg.WinH = (int)(Height / Ui.Scale);
                _cfg.LastSelectedId = _tree.SelectedId;
            }
            catch { }
            _cfg.Save();

            if (_pm.AnyRunning())
            {
                if (_cfg.StopAllOnExit)
                {
                    // 设置就是"退出时停止"，不用再问；崩溃/强杀的情况由 Job Object 兜底
                    _pm.StopAll(true);
                }
                else
                {
                    var r = MessageBox.Show(this,
                        "还有 jar 在运行。\n\n要现在把它们停掉吗？\n（选「否」的话它们会继续在后台跑，变成孤儿进程）",
                        "JarStart", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.OK) _pm.StopAll(true);
                }
            }
            ProcessManager.ShutdownJob();
            if (_tray != null) { _tray.Dispose(); _tray = null; }
            base.OnFormClosing(e);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5) { StartSelected(); return true; }
            if (keyData == Keys.F6) { StopSelected(); return true; }
            if (keyData == Keys.Delete) { RemoveSelected(); return true; }
            if (keyData == Keys.Control && keyData == Keys.F) { _search.Box.Focus(); _search.Box.SelectAll(); return true; }
            if (keyData == Keys.F7) { RestartSelected(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
