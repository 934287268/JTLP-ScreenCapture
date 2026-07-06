using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JTLP;

/// <summary>
/// 应用配置模型。
/// </summary>
public class AppSettings
{
    private static readonly string ConfigDir = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string ConfigFile = Path.Combine(ConfigDir, "jtlp-settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    // ==================== 截图设置 ====================
    /// <summary>截图保存目录（空=exe同级"截图中转站"）。</summary>
    public string SavePath { get; set; } = string.Empty;

    // ==================== 录屏设置 ====================
    /// <summary>录屏保存目录（空=exe同级"录屏中转站"）。</summary>
    public string RecordSavePath { get; set; } = string.Empty;

    /// <summary>录屏分辨率（如 "原始", "1920x1080", "1280x720", "854x480"）。</summary>
    public string RecordResolution { get; set; } = "原始";

    /// <summary>自动分段时长（分钟），0=不自动分段。</summary>
    public int SegmentMinutes { get; set; } = 10;

    /// <summary>录屏压缩质量（高/中/低）。</summary>
    public string RecordQuality { get; set; } = "中";

    // ==================== 通用设置 ====================
    /// <summary>是否开机自动启动。</summary>
    public bool AutoStart { get; set; } = false;

    // ==================== 热键设置 ====================
    /// <summary>全屏截图热键（如 "Alt+1"）。</summary>
    public string HotkeyFullScreen { get; set; } = "Alt+1";

    /// <summary>开始录屏热键（如 "Alt+2"）。</summary>
    public string HotkeyRecordStart { get; set; } = "Alt+2";

    /// <summary>停止录屏热键（如 "Alt+3"）。</summary>
    public string HotkeyRecordStop { get; set; } = "Alt+3";

    /// <summary>
    /// 获取截图实际生效的保存目录。
    /// </summary>
    public string GetEffectiveSavePath()
    {
        if (!string.IsNullOrWhiteSpace(SavePath))
            return SavePath;
        return Path.Combine(ConfigDir, "截图中转站");
    }

    /// <summary>
    /// 获取录屏实际生效的保存目录。
    /// </summary>
    public string GetEffectiveRecordSavePath()
    {
        if (!string.IsNullOrWhiteSpace(RecordSavePath))
            return RecordSavePath;
        return Path.Combine(ConfigDir, "录屏中转站");
    }

    /// <summary>
    /// 获取截图默认保存目录（显示用）。
    /// </summary>
    public string GetDefaultSavePath() => Path.Combine(ConfigDir, "截图中转站");

    /// <summary>
    /// 获取录屏默认保存目录（显示用）。
    /// </summary>
    public string GetDefaultRecordSavePath() => Path.Combine(ConfigDir, "录屏中转站");

    /// <summary>
    /// 应用开机自启动设置到 Windows 注册表。
    /// </summary>
    public void ApplyAutoStart()
    {
        const string regKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "JTLP";
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regKey, true);
            if (key == null) return;

            if (AutoStart && !string.IsNullOrEmpty(exePath))
            {
                key.SetValue(appName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(appName, false);
            }
        }
        catch { }
    }

    /// <summary>
    /// 从文件加载配置，不存在则返回默认值。
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (s != null) return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    /// <summary>
    /// 保存到文件。
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(ConfigFile, json);
        }
        catch { }
    }
}
