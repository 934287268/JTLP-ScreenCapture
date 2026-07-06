using System.Diagnostics;

namespace JTLP;

/// <summary>
/// 基于 FFmpeg 的屏幕录制器（支持自动分段 + 录后合并）。
/// </summary>
public class ScreenRecorder : IDisposable
{
    private Process? _ffmpegProcess;
    private string? _outputPath;
    private string? _segmentDir;
    private readonly string _recordDir;
    private readonly string _ffmpegPath;
    private string _lastError = string.Empty;
    private int _segmentMinutes = 10;

    public bool IsRecording => _ffmpegProcess != null && !_ffmpegProcess.HasExited;
    public string? CurrentFile => _outputPath;
    public string LastError => _lastError;

    public ScreenRecorder(string recordDir)
    {
        _recordDir = recordDir;
        Directory.CreateDirectory(_recordDir);

        // 查找 ffmpeg
        var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "ffmpeg", "ffmpeg.exe");
        if (File.Exists(toolsPath))
        {
            _ffmpegPath = toolsPath;
            return;
        }

        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            dir = Path.GetDirectoryName(dir)!;
            if (dir == null) break;
            var candidate = Path.Combine(dir, "tools", "ffmpeg", "ffmpeg.exe");
            if (File.Exists(candidate)) { _ffmpegPath = candidate; return; }
        }

        _ffmpegPath = "ffmpeg";
    }

    /// <summary>
    /// 开始全屏录制（自动分段）。
    /// </summary>
    /// <param name="resolution">录屏分辨率。</param>
    /// <param name="segmentMinutes">分段时长（分钟），0=不自动分段。</param>
    /// <param name="quality">压缩质量：高/中/低。</param>
    public bool StartRecording(string resolution = "原始", int segmentMinutes = 10, string quality = "中")
    {
        if (IsRecording) return false;

        _lastError = string.Empty;
        _segmentMinutes = segmentMinutes > 0 ? segmentMinutes : 10;

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // 根据质量设置 CRF 和 preset
        var (crf, preset) = quality switch
        {
            "高" => (18, "fast"),      // 高质量：文件大，CPU 中等
            "低" => (28, "ultrafast"), // 低质量：文件小，CPU 最低
            _ => (23, "ultrafast")     // 中等：默认平衡
        };

        if (segmentMinutes > 0)
        {
            _segmentDir = Path.Combine(_recordDir, $"segments_{timestamp}");
            Directory.CreateDirectory(_segmentDir);

            var segmentPattern = Path.Combine(_segmentDir, "seg_%03d.mp4");
            var videoSizeArg = resolution != "原始" ? $" -video_size {resolution}" : "";
            var args = $"-f gdigrab -framerate 30{videoSizeArg} -i desktop -c:v libx264 -preset {preset} -crf {crf} -pix_fmt yuv420p " +
                       $"-f segment -segment_time {segmentMinutes * 60} -reset_timestamps 1 \"{segmentPattern}\"";

            if (!StartFFmpegProcess(args)) return false;
            _outputPath = Path.Combine(_recordDir, $"录屏_{timestamp}.mp4");
        }
        else
        {
            _segmentDir = null;
            _outputPath = Path.Combine(_recordDir, $"录屏_{timestamp}.mp4");

            var videoSizeArg = resolution != "原始" ? $" -video_size {resolution}" : "";
            var args = $"-f gdigrab -framerate 30{videoSizeArg} -i desktop -c:v libx264 -preset {preset} -crf {crf} -pix_fmt yuv420p \"{_outputPath}\"";

            if (!StartFFmpegProcess(args)) return false;
        }

        return true;
    }

    private bool StartFFmpegProcess(string args)
    {
        try
        {
            _ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            _ffmpegProcess.Start();

            _ffmpegProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _lastError += e.Data + "\n";
            };
            _ffmpegProcess.BeginErrorReadLine();

            return true;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            CleanupProcess();
            return false;
        }
    }

    /// <summary>
    /// 检查 FFmpeg 是否仍在运行。
    /// </summary>
    public bool IsFFmpegAlive()
    {
        if (_ffmpegProcess == null) return false;
        if (_ffmpegProcess.HasExited)
        {
            _lastError = $"FFmpeg 已退出 (code={_ffmpegProcess.ExitCode})\n{_lastError}";
            CleanupProcess();
            return false;
        }
        return true;
    }

    /// <summary>
    /// 停止录制并合并分段文件。
    /// </summary>
    public bool StopRecording()
    {
        if (_ffmpegProcess == null || _ffmpegProcess.HasExited) return false;

        try
        {
            _ffmpegProcess.StandardInput.Write('q');
            _ffmpegProcess.StandardInput.Flush();

            if (!_ffmpegProcess.WaitForExit(5000))
                _ffmpegProcess.Kill();

            CleanupProcess();

            // 如果有分段，合并为一个文件
            if (_segmentDir != null && Directory.Exists(_segmentDir))
            {
                MergeSegments();
            }

            return true;
        }
        catch
        {
            try { _ffmpegProcess?.Kill(); } catch { }
            CleanupProcess();
            return false;
        }
    }

    /// <summary>
    /// 将分段文件合并为一个完整文件。
    /// </summary>
    private void MergeSegments()
    {
        try
        {
            if (_segmentDir == null || !Directory.Exists(_segmentDir)) return;

            var segments = Directory.GetFiles(_segmentDir, "seg_*.mp4")
                .OrderBy(f => f)
                .ToArray();

            if (segments.Length == 0)
            {
                _lastError = "没有找到分段文件";
                return;
            }

            // 如果只有一个分段，直接重命名
            if (segments.Length == 1)
            {
                File.Move(segments[0], _outputPath!, true);
                CleanupSegmentDir();
                return;
            }

            // 创建文件列表
            var listFile = Path.Combine(_segmentDir, "filelist.txt");
            File.WriteAllLines(listFile, segments.Select(f => $"file '{Path.GetFileName(f)}'"));

            // 用 FFmpeg 合并
            var args = $"-f concat -safe 0 -i \"{listFile}\" -c copy \"{_outputPath}\"";
            var mergeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    WorkingDirectory = _segmentDir
                }
            };

            mergeProcess.Start();
            var stderr = mergeProcess.StandardError.ReadToEnd();
            mergeProcess.WaitForExit(30000);

            if (mergeProcess.ExitCode != 0)
            {
                _lastError = $"合并失败: {stderr}";
                // 合并失败时保留分段文件
                return;
            }

            CleanupSegmentDir();
        }
        catch (Exception ex)
        {
            _lastError = $"合并异常: {ex.Message}";
        }
    }

    private void CleanupSegmentDir()
    {
        try
        {
            if (_segmentDir != null && Directory.Exists(_segmentDir))
                Directory.Delete(_segmentDir, true);
        }
        catch { }
    }

    private void CleanupProcess()
    {
        _ffmpegProcess?.Dispose();
        _ffmpegProcess = null;
    }

    public void Dispose()
    {
        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
        {
            try
            {
                _ffmpegProcess.StandardInput.Write('q');
                _ffmpegProcess.StandardInput.Flush();
                if (!_ffmpegProcess.WaitForExit(3000))
                    _ffmpegProcess.Kill();
            }
            catch { }
        }
        CleanupProcess();
    }
}
