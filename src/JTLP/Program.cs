using System.Runtime.InteropServices;

namespace JTLP;

/// <summary>
/// JTLP 截屏录屏工具。
/// Alt+1 全屏截图，Alt+2 开始录屏，Alt+3 停止录屏。
/// </summary>
internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        // 单实例校验
        const string mutexName = "Global\\JTLP_SingleInstance";
        _mutex = new Mutex(true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            MessageBox.Show("JTLP 已经在运行中，请检查系统托盘区域。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            using var mainForm = new MainForm();
            Application.Run(mainForm);
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}

/// <summary>
/// 系统托盘主窗体。
/// </summary>
public class MainForm : Form
{
    private NotifyIcon _notifyIcon = null!;
    private ContextMenuStrip _contextMenu = null!;
    private ToolStripMenuItem _screenshotItem = null!;
    private ToolStripMenuItem _startRecordItem = null!;
    private ToolStripMenuItem _stopRecordItem = null!;
    private readonly AppSettings _settings;
    private string _saveDir;
    private string _recordDir;
    private bool _settingsFormOpen;

    // ==================== 录屏器 ====================
    private ScreenRecorder? _recorder;
    private System.Windows.Forms.Timer? _recordingTimer;
    private DateTime _recordingStartTime;

    // ==================== 热键（user32） ====================
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_FULLSCREEN = 1;
    private const int HOTKEY_RECORD_START = 2;
    private const int HOTKEY_RECORD_STOP = 3;

    // 热键解析缓存
    private uint _screenshotMod;
    private Keys _screenshotKey;
    private uint _recordStartMod;
    private Keys _recordStartKey;
    private uint _recordStopMod;
    private Keys _recordStopKey;

    public MainForm()
    {
        _settings = AppSettings.Load();
        _saveDir = _settings.GetEffectiveSavePath();
        _recordDir = _settings.GetEffectiveRecordSavePath();
        Directory.CreateDirectory(_saveDir);
        Directory.CreateDirectory(_recordDir);

        InitializeForm();
        InitializeNotifyIcon();
        ParseAllHotkeys();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ShowSettings();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyHotkeys();
    }

    private void InitializeForm()
    {
        Text = "JTLP";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Opacity = 0;
        Size = new Size(1, 1);
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-32000, -32000);
    }

    private void InitializeNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "JTLP 截屏录屏工具",
            Visible = true
        };

        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.FromArgb(66, 133, 244));
        g.FillRectangle(brush, 2, 4, 12, 9);
        g.FillEllipse(brush, 5, 6, 6, 6);
        using var wb = new SolidBrush(Color.White);
        g.FillEllipse(wb, 6, 7, 4, 4);
        _notifyIcon.Icon = Icon.FromHandle(bmp.GetHicon());

        // 右键菜单
        _contextMenu = new ContextMenuStrip();

        _screenshotItem = new ToolStripMenuItem($"截图 ({_settings.HotkeyFullScreen})");
        _screenshotItem.Click += (_, _) => DoFullScreenCapture();
        _contextMenu.Items.Add(_screenshotItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        _startRecordItem = new ToolStripMenuItem($"开始录屏 ({_settings.HotkeyRecordStart})");
        _startRecordItem.Click += (_, _) => StartRecording();
        _contextMenu.Items.Add(_startRecordItem);

        _stopRecordItem = new ToolStripMenuItem($"停止录屏 ({_settings.HotkeyRecordStop})");
        _stopRecordItem.Enabled = false;
        _stopRecordItem.Click += (_, _) => StopRecording();
        _contextMenu.Items.Add(_stopRecordItem);

        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("设置...", null, (_, _) => ShowSettings());
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("退出", null, (_, _) => ExitApp());
        _notifyIcon.ContextMenuStrip = _contextMenu;

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                BeginInvoke(() => ShowSettings());
        };

        _recordingTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _recordingTimer.Tick += (_, _) => UpdateRecordingTooltip();
    }

    // ==================== 热键解析 ====================

    private void ParseAllHotkeys()
    {
        ParseHotkey(_settings.HotkeyFullScreen, out _screenshotMod, out _screenshotKey);
        ParseHotkey(_settings.HotkeyRecordStart, out _recordStartMod, out _recordStartKey);
        ParseHotkey(_settings.HotkeyRecordStop, out _recordStopMod, out _recordStopKey);
    }

    private static void ParseHotkey(string hotkeyStr, out uint modifiers, out Keys key)
    {
        modifiers = 0;
        key = Keys.None;

        var parts = hotkeyStr.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return;

        foreach (var part in parts[..^1])
        {
            switch (part.ToLower())
            {
                case "alt": modifiers |= 0x0001; break;
                case "ctrl" or "control": modifiers |= 0x0002; break;
                case "shift": modifiers |= 0x0004; break;
                case "win": modifiers |= 0x0008; break;
            }
        }

        var lastKey = parts[^1];
        key = lastKey switch
        {
            "0" => Keys.D0, "1" => Keys.D1, "2" => Keys.D2, "3" => Keys.D3,
            "4" => Keys.D4, "5" => Keys.D5, "6" => Keys.D6, "7" => Keys.D7,
            "8" => Keys.D8, "9" => Keys.D9,
            "F1" => Keys.F1, "F2" => Keys.F2, "F3" => Keys.F3, "F4" => Keys.F4,
            "F5" => Keys.F5, "F6" => Keys.F6, "F7" => Keys.F7, "F8" => Keys.F8,
            "F9" => Keys.F9, "F10" => Keys.F10, "F11" => Keys.F11, "F12" => Keys.F12,
            "PrtSc" => Keys.PrintScreen,
            _ => Enum.TryParse<Keys>(lastKey, true, out var k) ? k : Keys.None
        };
    }

    private void ApplyHotkeys()
    {
        UnregisterAll();

        var conflicts = new List<string>();

        if (_screenshotKey != Keys.None)
        {
            if (!RegisterHotKey(Handle, HOTKEY_FULLSCREEN, _screenshotMod, _screenshotKey))
                conflicts.Add($"截图热键: {_settings.HotkeyFullScreen}");
        }

        if (_recordStartKey != Keys.None)
        {
            if (!RegisterHotKey(Handle, HOTKEY_RECORD_START, _recordStartMod, _recordStartKey))
                conflicts.Add($"开始录屏热键: {_settings.HotkeyRecordStart}");
        }

        if (_recordStopKey != Keys.None)
        {
            if (!RegisterHotKey(Handle, HOTKEY_RECORD_STOP, _recordStopMod, _recordStopKey))
                conflicts.Add($"停止录屏热键: {_settings.HotkeyRecordStop}");
        }

        if (conflicts.Count > 0)
        {
            var msg = "以下热键被其他程序占用，无法注册：\n\n" +
                      string.Join("\n", conflicts) +
                      "\n\n请在设置中更换热键。";
            MessageBox.Show(msg, "JTLP - 热键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UnregisterAll()
    {
        UnregisterHotKey(Handle, HOTKEY_FULLSCREEN);
        UnregisterHotKey(Handle, HOTKEY_RECORD_START);
        UnregisterHotKey(Handle, HOTKEY_RECORD_STOP);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case HOTKEY_FULLSCREEN:
                    DoFullScreenCapture();
                    break;
                case HOTKEY_RECORD_START:
                    StartRecording();
                    break;
                case HOTKEY_RECORD_STOP:
                    StopRecording();
                    break;
            }
        }
        base.WndProc(ref m);
    }

    // ==================== 全屏截图 ====================

    private void DoFullScreenCapture()
    {
        try
        {
            var screens = Screen.AllScreens;
            int minX = screens.Min(s => s.Bounds.X);
            int minY = screens.Min(s => s.Bounds.Y);
            int maxX = screens.Max(s => s.Bounds.Right);
            int maxY = screens.Max(s => s.Bounds.Bottom);

            using var bitmap = new Bitmap(maxX - minX, maxY - minY);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(minX, minY, 0, 0, bitmap.Size);

            var filePath = Path.Combine(_saveDir, $"JTLP_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

            _notifyIcon.ShowBalloonTip(2000, "JTLP", "JT right", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(3000, "JTLP 错误", $"截图失败: {ex.Message}", ToolTipIcon.Error);
        }
    }

    // ==================== 录屏 ====================

    private void StartRecording()
    {
        if (_recorder != null && _recorder.IsRecording) return;

        try
        {
            _recorder ??= new ScreenRecorder(_recordDir);

            if (_recorder.StartRecording(_settings.RecordResolution, _settings.SegmentMinutes, _settings.RecordQuality))
            {
                _recordingStartTime = DateTime.Now;
                UpdateMenuState(recording: true);
                _notifyIcon.ShowBalloonTip(2000, "JTLP", "LP start", ToolTipIcon.Info);

                // 1秒后检查 FFmpeg 是否存活
                System.Windows.Forms.Timer? checkTimer = null;
                checkTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                checkTimer.Tick += (_, _) =>
                {
                    checkTimer.Stop();
                    checkTimer.Dispose();

                    if (_recorder != null && !_recorder.IsFFmpegAlive())
                    {
                        UpdateMenuState(recording: false);
                        _notifyIcon.ShowBalloonTip(5000, "JTLP 录屏失败",
                            $"FFmpeg 启动后退出\n{_recorder.LastError}", ToolTipIcon.Error);
                    }
                    else
                    {
                        _recordingTimer?.Start();
                    }
                };
                checkTimer.Start();
            }
            else
            {
                var err = _recorder.LastError;
                _notifyIcon.ShowBalloonTip(5000, "JTLP",
                    $"录屏启动失败{(string.IsNullOrEmpty(err) ? "" : $": {err}")}", ToolTipIcon.Error);
            }
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            _notifyIcon.ShowBalloonTip(5000, "JTLP 录屏错误", msg, ToolTipIcon.Error);
        }
    }

    private void StopRecording()
    {
        if (_recorder == null || !_recorder.IsRecording) return;

        try
        {
            _recorder.StopRecording();
            _recordingTimer?.Stop();
            UpdateMenuState(recording: false);

            var elapsed = DateTime.Now - _recordingStartTime;
            _notifyIcon.ShowBalloonTip(2000, "JTLP", "LP ending", ToolTipIcon.Info);
            _notifyIcon.Text = "JTLP 截屏录屏工具";
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(3000, "JTLP", $"停止录屏失败: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private void UpdateMenuState(bool recording)
    {
        _startRecordItem.Enabled = !recording;
        _stopRecordItem.Enabled = recording;
    }

    private void UpdateRecordingTooltip()
    {
        if (_recorder == null || !_recorder.IsRecording)
        {
            _recordingTimer?.Stop();
            return;
        }
        var elapsed = DateTime.Now - _recordingStartTime;
        _notifyIcon.Text = $"JTLP - 录制中 {elapsed:hh\\:mm\\:ss}";
    }

    // ==================== 设置 ====================

    private void ShowSettings()
    {
        if (_settingsFormOpen) return;
        _settingsFormOpen = true;

        SettingsForm? settingsForm = null;
        try
        {
            settingsForm = new SettingsForm(_settings);
            settingsForm.ShowDialog();

            // 设置关闭后重新应用
            _saveDir = _settings.GetEffectiveSavePath();
            _recordDir = _settings.GetEffectiveRecordSavePath();
            Directory.CreateDirectory(_saveDir);
            Directory.CreateDirectory(_recordDir);

            ParseAllHotkeys();
            ApplyHotkeys();
            _settings.ApplyAutoStart();

            _screenshotItem.Text = $"截图 ({_settings.HotkeyFullScreen})";
            _startRecordItem.Text = $"开始录屏 ({_settings.HotkeyRecordStart})";
            _stopRecordItem.Text = $"停止录屏 ({_settings.HotkeyRecordStop})";
            _notifyIcon.ShowBalloonTip(1500, "JTLP", "设置已生效", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(3000, "JTLP", $"设置失败: {ex.Message}", ToolTipIcon.Error);
        }
        finally
        {
            try { settingsForm?.Dispose(); } catch { }
            _settingsFormOpen = false;
        }
    }

    // ==================== 退出 ====================

    private void ExitApp()
    {
        _recorder?.Dispose();
        _recordingTimer?.Dispose();
        _notifyIcon.Visible = false;
        UnregisterAll();
        Application.Exit();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterAll();
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recorder?.Dispose();
            _recordingTimer?.Dispose();
            UnregisterAll();
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
        }
        base.Dispose(disposing);
    }
}
