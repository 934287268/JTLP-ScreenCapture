namespace JTLP;

/// <summary>
/// 设置窗口 — 左右叠代（ListBox 导航 + 右侧 Panel 面板切换）。
/// </summary>
public class SettingsForm : Form
{
    private readonly AppSettings _settings;

    // ==================== 控件 ====================
    private ListBox _navList = null!;
    private Panel _contentPanel = null!;
    private Button _btnSave = null!, _btnCancel = null!;
    private Button _btnMinimize = null!, _btnExit = null!;

    // 热键面板
    private TextBox _txtHotkeyFullScreen = null!;
    private TextBox _txtHotkeyRecordStart = null!;
    private TextBox _txtHotkeyRecordStop = null!;

    // 存储面板
    private TextBox _txtSavePath = null!;
    private TextBox _txtRecordSavePath = null!;
    private Button _btnBrowseSave = null!;
    private Button _btnBrowseRecord = null!;
    private ComboBox _cbRecordResolution = null!;
    private NumericUpDown _nudSegmentMinutes = null!;
    private ComboBox _cbRecordQuality = null!;

    // 通用面板
    private CheckBox _chkAutoStart = null!;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        BuildUi();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _navList.SelectedIndex = 0;  // 先触发面板创建
        LoadSettings();              // 再给控件赋值
    }

    // ==================== 构建 UI ====================

    private void BuildUi()
    {
        SuspendLayout();
        Font = new Font("Microsoft YaHei", 9f);

        Text = "JTLP - 设置";
        ClientSize = new Size(580, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;

        // ---- 左侧导航 ----
        _navList = new ListBox
        {
            Location = new Point(10, 10),
            Size = new Size(90, 400),
            BorderStyle = BorderStyle.FixedSingle,
            IntegralHeight = false,
            ItemHeight = 24
        };
        _navList.Items.AddRange(new[] { "热键", "存储", "通用" });
        _navList.SelectedIndexChanged += (_, _) => SwitchPanel();

        // ---- 右侧内容面板 ----
        _contentPanel = new Panel
        {
            Location = new Point(110, 10),
            Size = new Size(460, 400),
            BorderStyle = BorderStyle.FixedSingle
        };

        // ---- 底部按钮 ----
        _btnSave = new Button { Text = "保存(&S)", Location = new Point(260, 430), Size = new Size(80, 30) };
        _btnSave.Click += OnSaveClick;

        _btnCancel = new Button { Text = "取消(&C)", Location = new Point(350, 430), Size = new Size(80, 30) };
        _btnCancel.Click += (_, _) => Close();

        _btnMinimize = new Button { Text = "最小化", Location = new Point(440, 430), Size = new Size(70, 30) };
        _btnMinimize.Click += (_, _) => { SaveSettings(); Close(); };

        _btnExit = new Button { Text = "退出(&X)", Location = new Point(515, 430), Size = new Size(75, 30) };
        _btnExit.Click += (_, _) => Application.Exit();

        Controls.AddRange(new Control[] { _navList, _contentPanel, _btnSave, _btnCancel, _btnMinimize, _btnExit });
        ResumeLayout(false);
        PerformLayout();
    }

    // ==================== 面板切换 ====================

    private void SwitchPanel()
    {
        _contentPanel.Controls.Clear();
        _contentPanel.SuspendLayout();

        switch (_navList.SelectedIndex)
        {
            case 0: BuildHotkeyPanel(); break;
            case 1: BuildStoragePanel(); break;
            case 2: BuildGeneralPanel(); break;
        }

        _contentPanel.ResumeLayout(false);
        _contentPanel.PerformLayout();

        // 切换后重新加载当前面板的设置值
        LoadSettings();
    }

    // ==================== 热键面板 ====================

    private void BuildHotkeyPanel()
    {
        AddLabel("全屏截图:", 15, 20);
        _txtHotkeyFullScreen = AddHotkeyInput(160, 17);

        AddLabel("开始录屏:", 15, 60);
        _txtHotkeyRecordStart = AddHotkeyInput(160, 57);

        AddLabel("停止录屏:", 15, 100);
        _txtHotkeyRecordStop = AddHotkeyInput(160, 97);

        var tip = new Label
        {
            Text = "点击输入框，按下新快捷键组合\n支持 Alt/Ctrl/Shift/Win + 字母/数字/F键",
            Location = new Point(15, 145),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        _contentPanel.Controls.Add(tip);
    }

    private TextBox AddHotkeyInput(int x, int y)
    {
        var txt = new TextBox { Location = new Point(x, y), Width = 200, ReadOnly = true, BackColor = Color.White };
        txt.KeyDown += OnHotkeyKeyDown;
        _contentPanel.Controls.Add(txt);
        return txt;
    }

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return;

        var parts = new List<string>();
        if (e.Control) parts.Add("Ctrl");
        if (e.Alt) parts.Add("Alt");
        if (e.Shift) parts.Add("Shift");
        parts.Add(GetKeyName(e.KeyCode));

        if (sender is TextBox txt)
            txt.Text = string.Join("+", parts);

        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    private static string GetKeyName(Keys key) => key switch
    {
        Keys.D0 => "0", Keys.D1 => "1", Keys.D2 => "2", Keys.D3 => "3", Keys.D4 => "4",
        Keys.D5 => "5", Keys.D6 => "6", Keys.D7 => "7", Keys.D8 => "8", Keys.D9 => "9",
        Keys.F1 => "F1", Keys.F2 => "F2", Keys.F3 => "F3", Keys.F4 => "F4",
        Keys.F5 => "F5", Keys.F6 => "F6", Keys.F7 => "F7", Keys.F8 => "F8",
        Keys.F9 => "F9", Keys.F10 => "F10", Keys.F11 => "F11", Keys.F12 => "F12",
        Keys.PrintScreen => "PrtSc",
        _ => key.ToString()
    };

    // ==================== 存储面板 ====================

    private void BuildStoragePanel()
    {
        AddLabel("截图保存目录:", 15, 15);
        _txtSavePath = new TextBox
        {
            Location = new Point(15, 38),
            Width = 320,
            PlaceholderText = _settings.GetDefaultSavePath()
        };
        _contentPanel.Controls.Add(_txtSavePath);

        _btnBrowseSave = new Button { Text = "...", Location = new Point(340, 37), Width = 30 };
        _btnBrowseSave.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "选择截图保存目录" };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _txtSavePath.Text = dlg.SelectedPath;
        };
        _contentPanel.Controls.Add(_btnBrowseSave);

        var btnCopySave = new Button { Text = "复制", Location = new Point(375, 37), Width = 40 };
        btnCopySave.Click += (_, _) =>
        {
            var path = string.IsNullOrWhiteSpace(_txtSavePath.Text) ? _settings.GetDefaultSavePath() : _txtSavePath.Text;
            Clipboard.SetText(path);
        };
        _contentPanel.Controls.Add(btnCopySave);

        var btnOpenSave = new Button { Text = "打开", Location = new Point(420, 37), Width = 40 };
        btnOpenSave.Click += (_, _) =>
        {
            var path = string.IsNullOrWhiteSpace(_txtSavePath.Text) ? _settings.GetDefaultSavePath() : _txtSavePath.Text;
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start("explorer.exe", path);
        };
        _contentPanel.Controls.Add(btnOpenSave);

        var lblSaveSpace = new Label
        {
            Location = new Point(15, 65),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        UpdateSpaceLabel(lblSaveSpace, string.IsNullOrWhiteSpace(_txtSavePath.Text) ? _settings.GetDefaultSavePath() : _txtSavePath.Text);
        _contentPanel.Controls.Add(lblSaveSpace);

        AddLabel("录屏保存目录:", 15, 100);
        _txtRecordSavePath = new TextBox
        {
            Location = new Point(15, 123),
            Width = 320,
            PlaceholderText = _settings.GetDefaultRecordSavePath()
        };
        _contentPanel.Controls.Add(_txtRecordSavePath);

        _btnBrowseRecord = new Button { Text = "...", Location = new Point(340, 122), Width = 30 };
        _btnBrowseRecord.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "选择录屏保存目录" };
            if (dlg.ShowDialog(this) == DialogResult.OK)
                _txtRecordSavePath.Text = dlg.SelectedPath;
        };
        _contentPanel.Controls.Add(_btnBrowseRecord);

        var btnCopyRecord = new Button { Text = "复制", Location = new Point(375, 122), Width = 40 };
        btnCopyRecord.Click += (_, _) =>
        {
            var path = string.IsNullOrWhiteSpace(_txtRecordSavePath.Text) ? _settings.GetDefaultRecordSavePath() : _txtRecordSavePath.Text;
            Clipboard.SetText(path);
        };
        _contentPanel.Controls.Add(btnCopyRecord);

        var btnOpenRecord = new Button { Text = "打开", Location = new Point(420, 122), Width = 40 };
        btnOpenRecord.Click += (_, _) =>
        {
            var path = string.IsNullOrWhiteSpace(_txtRecordSavePath.Text) ? _settings.GetDefaultRecordSavePath() : _txtRecordSavePath.Text;
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start("explorer.exe", path);
        };
        _contentPanel.Controls.Add(btnOpenRecord);

        var lblRecordSpace = new Label
        {
            Location = new Point(15, 150),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        UpdateSpaceLabel(lblRecordSpace, string.IsNullOrWhiteSpace(_txtRecordSavePath.Text) ? _settings.GetDefaultRecordSavePath() : _txtRecordSavePath.Text);
        _contentPanel.Controls.Add(lblRecordSpace);

        AddLabel("录制分辨率:", 15, 180);
        _cbRecordResolution = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(160, 177),
            Width = 200
        };
        _cbRecordResolution.Items.AddRange(new object[] { "原始（屏幕实际分辨率）", "1920x1080", "1280x720", "854x480" });
        _contentPanel.Controls.Add(_cbRecordResolution);

        AddLabel("自动分段时长:", 15, 215);
        _nudSegmentMinutes = new NumericUpDown
        {
            Location = new Point(160, 212),
            Width = 60,
            Minimum = 0,
            Maximum = 60,
            Value = 10
        };
        _contentPanel.Controls.Add(_nudSegmentMinutes);

        var lblSegmentUnit = new Label
        {
            Text = "分钟（0=不分段，录制后自动合并）",
            Location = new Point(225, 215),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        _contentPanel.Controls.Add(lblSegmentUnit);

        AddLabel("压缩质量:", 15, 250);
        _cbRecordQuality = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(160, 247),
            Width = 200
        };
        _cbRecordQuality.Items.AddRange(new object[] { "高（画质好，文件大，CPU中等）", "中（平衡，默认）", "低（文件小，CPU最低）" });
        _contentPanel.Controls.Add(_cbRecordQuality);

        var tip = new Label
        {
            Text = "留空则使用默认目录\n截图：程序目录/截图中转站\n录屏：程序目录/录屏中转站",
            Location = new Point(15, 290),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        _contentPanel.Controls.Add(tip);
    }

    // ==================== 通用面板 ====================

    private void BuildGeneralPanel()
    {
        _chkAutoStart = new CheckBox
        {
            Text = "开机自动启动",
            Location = new Point(15, 20),
            AutoSize = true
        };
        _contentPanel.Controls.Add(_chkAutoStart);

        var tip = new Label
        {
            Text = "打勾后下次开机时 JTLP 将自动启动\n（通过 Windows 注册表实现）",
            Location = new Point(15, 55),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        _contentPanel.Controls.Add(tip);
    }

    // ==================== 辅助 ====================

    private void AddLabel(string text, int x, int y)
    {
        var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        _contentPanel.Controls.Add(lbl);
    }

    private static void UpdateSpaceLabel(Label lbl, string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:\\");
            var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            lbl.Text = $"剩余空间: {freeGB:F1} GB";
            lbl.ForeColor = freeGB < 1 ? Color.Red : Color.Gray;
        }
        catch
        {
            lbl.Text = "剩余空间: 无法检测";
            lbl.ForeColor = Color.Gray;
        }
    }

    // ==================== 加载 & 保存 ====================

    private void LoadSettings()
    {
        if (_txtHotkeyFullScreen != null)
            _txtHotkeyFullScreen.Text = _settings.HotkeyFullScreen;
        if (_txtHotkeyRecordStart != null)
            _txtHotkeyRecordStart.Text = _settings.HotkeyRecordStart;
        if (_txtHotkeyRecordStop != null)
            _txtHotkeyRecordStop.Text = _settings.HotkeyRecordStop;
        if (_txtSavePath != null)
            _txtSavePath.Text = _settings.SavePath;
        if (_txtRecordSavePath != null)
            _txtRecordSavePath.Text = _settings.RecordSavePath;
        if (_chkAutoStart != null)
            _chkAutoStart.Checked = _settings.AutoStart;
        if (_cbRecordResolution != null)
        {
            var resIdx = _settings.RecordResolution switch
            {
                "1920x1080" => 1,
                "1280x720" => 2,
                "854x480" => 3,
                _ => 0
            };
            _cbRecordResolution.SelectedIndex = resIdx;
        }
        if (_nudSegmentMinutes != null)
            _nudSegmentMinutes.Value = Math.Clamp(_settings.SegmentMinutes, 0, 60);
        if (_cbRecordQuality != null)
        {
            var qualityIdx = _settings.RecordQuality switch
            {
                "高" => 0,
                "低" => 2,
                _ => 1
            };
            _cbRecordQuality.SelectedIndex = qualityIdx;
        }
    }

    private void SaveSettings()
    {
        if (_txtHotkeyFullScreen != null)
            _settings.HotkeyFullScreen = _txtHotkeyFullScreen.Text;
        if (_txtHotkeyRecordStart != null)
            _settings.HotkeyRecordStart = _txtHotkeyRecordStart.Text;
        if (_txtHotkeyRecordStop != null)
            _settings.HotkeyRecordStop = _txtHotkeyRecordStop.Text;
        if (_txtSavePath != null)
            _settings.SavePath = _txtSavePath.Text;
        if (_txtRecordSavePath != null)
            _settings.RecordSavePath = _txtRecordSavePath.Text;
        if (_chkAutoStart != null)
            _settings.AutoStart = _chkAutoStart.Checked;
        if (_cbRecordResolution != null)
        {
            _settings.RecordResolution = _cbRecordResolution.SelectedIndex switch
            {
                1 => "1920x1080",
                2 => "1280x720",
                3 => "854x480",
                _ => "原始"
            };
        }
        if (_nudSegmentMinutes != null)
            _settings.SegmentMinutes = (int)_nudSegmentMinutes.Value;
        if (_cbRecordQuality != null)
        {
            _settings.RecordQuality = _cbRecordQuality.SelectedIndex switch
            {
                0 => "高",
                2 => "低",
                _ => "中"
            };
        }
        _settings.Save();
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        SaveSettings();
        Close();
    }
}
