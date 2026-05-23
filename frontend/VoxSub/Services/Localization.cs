using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace VoxSub.Services;

public sealed class Localization : INotifyPropertyChanged
{
    public static Localization Instance { get; } = new();

    public const string SystemCulture = "System";
    public const string DefaultCulture = "zh-CN";
    public const string EnglishCulture = "en-US";

    private string _currentCulture = DefaultCulture;

    private readonly Dictionary<string, Dictionary<string, string>> _resources = new()
    {
        ["zh-CN"] = new()
        {
            // Window
            ["WindowTitle"] = "VoxSub",
            ["WindowSubtitle"] = "字幕工具",

            // Status
            ["StatusLabel"] = "状态",
            ["StatusIdle"] = "空闲",
            ["StatusPreparing"] = "准备环境…",
            ["StatusEnvironmentNotReady"] = "环境未就绪",
            ["StatusToolchainMissing"] = "工具链缺失",
            ["StatusRunning"] = "运行中…",
            ["StatusSuccess"] = "成功",
            ["StatusFailed"] = "失败",
            ["StatusCancelled"] = "已取消",
            ["StatusError"] = "异常",
            ["StatusParamError"] = "参数错误",

            // Buttons
            ["Start"] = "开始",
            ["Cancel"] = "取消",
            ["ClearLog"] = "清空日志",
            ["Settings"] = "设置",
            ["Browse"] = "浏览...",
            ["Default"] = "默认",
            ["RefreshDetect"] = "重新检测",
            ["Close"] = "关闭",
            ["Apply"] = "应用",
            ["Save"] = "保存",

            // Sections
            ["CommandSection"] = "命令",
            ["FileSection"] = "文件",
            ["ParametersSection"] = "参数",
            ["LogOutputSection"] = "日志输出",
            ["BinaryPathsSection"] = "二进制文件路径",

            // Command radio
            ["TranscribeSubtitle"] = "转写字幕",
            ["EmbedSubtitle"] = "封装字幕",
            ["TranscribeAndEmbed"] = "转写并封装",

            // File fields
            ["MediaFile"] = "媒体文件",
            ["MediaFilePlaceholder"] = "选择媒体文件",
            ["SubtitleFile"] = "字幕文件",
            ["SubtitleFilePlaceholder"] = "默认同名 .srt",
            ["SrtOutput"] = "SRT 输出",
            ["SrtOutputPlaceholder"] = "默认同名 .srt",
            ["MkvOutput"] = "MKV 输出",
            ["MkvOutputPlaceholder"] = "默认同名 .mkv",

            // Parameters
            ["Language"] = "语言",
            ["SystemLanguage"] = "跟随系统",
            ["Chinese"] = "简体中文",
            ["English"] = "English",
            ["Model"] = "模型",
            ["Device"] = "设备",
            ["Fp16"] = "fp16",
            ["Verbose"] = "显示 Whisper 详细输出 (--verbose)",
            ["Overwrite"] = "覆盖已有输出文件 (--overwrite)",

            // Log
            ["FollowLog"] = "跟随最新日志",

            // About
            ["AboutTitle"] = "关于",

            // Settings
            ["SettingsTitle"] = "VoxSub 设置",
            ["SettingsSubtitle"] = "工具路径与语言",
            ["Ffmpeg"] = "ffmpeg",
            ["FfmpegPlaceholder"] = "ffmpeg.exe",
            ["Python"] = "Python",
            ["PythonPlaceholder"] = "python.exe",
            ["VoxSub"] = "VoxSub",
            ["VoxSubPlaceholder"] = "voxsub.py",
            ["LanguageSettings"] = "语言设置",

            // Dialogs
            ["SelectMediaFile"] = "选择媒体文件",
            ["SelectSubtitleFile"] = "选择字幕文件",
            ["SelectSrtOutput"] = "选择 SRT 输出路径",
            ["SelectMkvOutput"] = "选择 MKV 输出路径",
            ["SelectFfmpeg"] = "选择 ffmpeg 可执行文件",
            ["SelectPython"] = "选择 Python 可执行文件",
            ["SelectVoxSub"] = "选择 VoxSub 命令或脚本",

            // File filters
            ["AllMediaFiles"] = "所有媒体文件",
            ["AllFiles"] = "所有文件",
            ["SrtSubtitles"] = "SRT 字幕",
            ["MkvVideos"] = "MKV 视频",
            ["ExecutableFiles"] = "可执行文件",
            ["PythonScripts"] = "Python 脚本",

            // Validation
            ["ErrorValidationFailed"] = "[错误] 参数校验失败：",
            ["ErrorSelectMedia"] = "请选择媒体文件。",
            ["ErrorMediaNotExists"] = "媒体文件不存在：",
            ["ErrorSubtitleNotExists"] = "字幕文件不存在：",
            ["ErrorSrtExists"] = "SRT 输出文件已存在：{0}（请勾选'覆盖已有文件'或更改输出路径）",
            ["ErrorMkvExists"] = "MKV 输出文件已存在：{0}（请勾选'覆盖已有文件'或更改输出路径）",
            ["ErrorFfmpegNotFound"] = "[错误] 未找到 ffmpeg。请在设置中选择 ffmpeg 可执行文件，或将 ffmpeg 加入 PATH。",
            ["SettingsSaved"] = "设置已应用。",
            ["SettingsSaveFailed"] = "保存失败：",

            // Execution
            ["InfoUsingFfmpeg"] = "[信息] 使用 ffmpeg：",
            ["InfoExecuting"] = "[信息] 执行命令：",
            ["CompletedSuccess"] = "[完成] 任务成功。",
            ["ErrorFailedExitCode"] = "[错误] 任务失败，退出码：",
            ["Cancelled"] = "[取消] 任务已被用户取消。",
            ["Exception"] = "[异常] ",
        },
        ["en-US"] = new()
        {
            // Window
            ["WindowTitle"] = "VoxSub",
            ["WindowSubtitle"] = "Subtitle Tool",

            // Status
            ["StatusLabel"] = "Status",
            ["StatusIdle"] = "Idle",
            ["StatusPreparing"] = "Preparing environment…",
            ["StatusEnvironmentNotReady"] = "Environment not ready",
            ["StatusToolchainMissing"] = "Toolchain missing",
            ["StatusRunning"] = "Running…",
            ["StatusSuccess"] = "Success",
            ["StatusFailed"] = "Failed",
            ["StatusCancelled"] = "Cancelled",
            ["StatusError"] = "Error",
            ["StatusParamError"] = "Parameter error",

            // Buttons
            ["Start"] = "Start",
            ["Cancel"] = "Cancel",
            ["ClearLog"] = "Clear Log",
            ["Settings"] = "Settings",
            ["Browse"] = "Browse...",
            ["Default"] = "Default",
            ["RefreshDetect"] = "Re-detect",
            ["Close"] = "Close",
            ["Apply"] = "Apply",
            ["Save"] = "Save",

            // Sections
            ["CommandSection"] = "Command",
            ["FileSection"] = "File",
            ["ParametersSection"] = "Parameters",
            ["LogOutputSection"] = "Log Output",
            ["BinaryPathsSection"] = "Binary Paths",

            // Command radio
            ["TranscribeSubtitle"] = "Transcribe",
            ["EmbedSubtitle"] = "Embed",
            ["TranscribeAndEmbed"] = "Transcribe & Embed",

            // File fields
            ["MediaFile"] = "Media File",
            ["MediaFilePlaceholder"] = "Select media file",
            ["SubtitleFile"] = "Subtitle File",
            ["SubtitleFilePlaceholder"] = "Default: same .srt",
            ["SrtOutput"] = "SRT Output",
            ["SrtOutputPlaceholder"] = "Default: same .srt",
            ["MkvOutput"] = "MKV Output",
            ["MkvOutputPlaceholder"] = "Default: same .mkv",

            // Parameters
            ["Language"] = "Language",
            ["SystemLanguage"] = "System",
            ["Chinese"] = "简体中文",
            ["English"] = "English",
            ["Model"] = "Model",
            ["Device"] = "Device",
            ["Fp16"] = "fp16",
            ["Verbose"] = "Show Whisper verbose output (--verbose)",
            ["Overwrite"] = "Overwrite existing output files (--overwrite)",

            // Log
            ["FollowLog"] = "Follow Log",

            // About
            ["AboutTitle"] = "About",

            // Settings
            ["SettingsTitle"] = "VoxSub Settings",
            ["SettingsSubtitle"] = "Tool Paths & Language",
            ["Ffmpeg"] = "ffmpeg",
            ["FfmpegPlaceholder"] = "ffmpeg.exe",
            ["Python"] = "Python",
            ["PythonPlaceholder"] = "python.exe",
            ["VoxSub"] = "VoxSub",
            ["VoxSubPlaceholder"] = "voxsub.py",
            ["LanguageSettings"] = "Language Settings",

            // Dialogs
            ["SelectMediaFile"] = "Select Media File",
            ["SelectSubtitleFile"] = "Select Subtitle File",
            ["SelectSrtOutput"] = "Select SRT Output Path",
            ["SelectMkvOutput"] = "Select MKV Output Path",
            ["SelectFfmpeg"] = "Select ffmpeg Executable",
            ["SelectPython"] = "Select Python Executable",
            ["SelectVoxSub"] = "Select VoxSub Command or Script",

            // File filters
            ["AllMediaFiles"] = "All Media Files",
            ["AllFiles"] = "All Files",
            ["SrtSubtitles"] = "SRT Subtitles",
            ["MkvVideos"] = "MKV Videos",
            ["ExecutableFiles"] = "Executable Files",
            ["PythonScripts"] = "Python Scripts",

            // Validation
            ["ErrorValidationFailed"] = "[Error] Parameter validation failed:",
            ["ErrorSelectMedia"] = "Please select a media file.",
            ["ErrorMediaNotExists"] = "Media file does not exist: ",
            ["ErrorSubtitleNotExists"] = "Subtitle file does not exist: ",
            ["ErrorSrtExists"] = "SRT output file already exists: {0} (check 'Overwrite' or change output path)",
            ["ErrorMkvExists"] = "MKV output file already exists: {0} (check 'Overwrite' or change output path)",
            ["ErrorFfmpegNotFound"] = "[Error] ffmpeg not found. Please select ffmpeg executable in Settings or add ffmpeg to PATH.",
            ["SettingsSaved"] = "Settings applied.",
            ["SettingsSaveFailed"] = "Save failed: ",

            // Execution
            ["InfoUsingFfmpeg"] = "[Info] Using ffmpeg: ",
            ["InfoExecuting"] = "[Info] Executing: ",
            ["CompletedSuccess"] = "[Done] Task completed successfully.",
            ["ErrorFailedExitCode"] = "[Error] Task failed, exit code: ",
            ["Cancelled"] = "[Cancelled] Task cancelled by user.",
            ["Exception"] = "[Exception] ",
        }
    };

    public string CurrentCulture
    {
        get => _currentCulture;
        set
        {
            var resolvedCulture = ResolveCulture(value);
            if (_currentCulture != resolvedCulture)
            {
                _currentCulture = resolvedCulture;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Strings));
            }
        }
    }

    public static string ResolveCulture(string? culture)
    {
        if (string.Equals(culture, SystemCulture, StringComparison.OrdinalIgnoreCase))
        {
            var systemCulture = CultureInfo.CurrentCulture.Name;
            return systemCulture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? DefaultCulture
                : EnglishCulture;
        }

        return string.Equals(culture, EnglishCulture, StringComparison.OrdinalIgnoreCase)
            ? EnglishCulture
            : DefaultCulture;
    }

    public string this[string key]
    {
        get
        {
            if (_resources.TryGetValue(_currentCulture, out var cultureResources) &&
                cultureResources.TryGetValue(key, out var value))
            {
                return value;
            }

            // Fallback to zh-CN
            if (_resources.TryGetValue("zh-CN", out var fallbackResources) &&
                fallbackResources.TryGetValue(key, out var fallbackValue))
            {
                return fallbackValue;
            }

            return $"[{key}]";
        }
    }

    public LocalizationStrings Strings => new(this);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class LocalizationStrings
{
    private readonly Localization _localization;

    public LocalizationStrings(Localization localization)
    {
        _localization = localization;
    }

    public string this[string key] => _localization[key];
}
