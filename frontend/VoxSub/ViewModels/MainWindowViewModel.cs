using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoxSub.Models;
using VoxSub.Services;

namespace VoxSub.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private static string L(string key) => Localization.Instance[key];

    // ----- 命令选择 -----
    [ObservableProperty]
    private VoxSubCommandKind _selectedCommandKind = VoxSubCommandKind.Transcribe;

    public bool IsTranscribeSelected
    {
        get => SelectedCommandKind == VoxSubCommandKind.Transcribe;
        set { if (value) SelectedCommandKind = VoxSubCommandKind.Transcribe; }
    }

    public bool IsEmbedSelected
    {
        get => SelectedCommandKind == VoxSubCommandKind.Embed;
        set { if (value) SelectedCommandKind = VoxSubCommandKind.Embed; }
    }

    public bool IsAllSelected
    {
        get => SelectedCommandKind == VoxSubCommandKind.All;
        set { if (value) SelectedCommandKind = VoxSubCommandKind.All; }
    }

    // ----- 文件路径 -----
    [ObservableProperty]
    private string _mediaPath = string.Empty;

    [ObservableProperty]
    private string _subtitlePath = string.Empty;

    [ObservableProperty]
    private string _outputSrtPath = string.Empty;

    [ObservableProperty]
    private string _outputVideoPath = string.Empty;

    // ----- 参数 -----
    [ObservableProperty]
    private string _language = "auto";

    public List<string> LanguageOptions { get; } = ["auto", "zh", "zh-Hans", "en", "ja", "ko", "fr", "de", "es"];

    [ObservableProperty]
    private string _model = "large";

    public List<string> ModelOptions { get; } = ["tiny", "base", "small", "medium", "large", "turbo"];

    [ObservableProperty]
    private string _device = "auto";

    public List<string> DeviceOptions { get; } = ["auto", "cuda", "mps", "cpu"];

    [ObservableProperty]
    private string _fp16 = "auto";

    public List<string> Fp16Options { get; } = ["auto", "true", "false"];

    [ObservableProperty]
    private bool _verbose;

    [ObservableProperty]
    private bool _overwrite;

    // ----- 状态 -----
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = L("StatusIdle");

    // ----- 日志 -----
    [ObservableProperty]
    private string _logText = string.Empty;

    // ----- 内部状态 -----
    private CancellationTokenSource? _cts;

    // ----- 控件启用/禁用 -----
    /// <summary>
    /// 是否允许修改输入控件（运行期间为 false）。
    /// </summary>
    public bool CanEdit => !IsRunning;

    /// <summary>
    /// 字幕文件选择是否启用（仅 embed 模式）。
    /// </summary>
    public bool IsEmbedMode => SelectedCommandKind == VoxSubCommandKind.Embed;

    /// <summary>
    /// SRT 输出是否启用（transcribe / all）。
    /// </summary>
    public bool IsOutputSrtEnabled => SelectedCommandKind is VoxSubCommandKind.Transcribe or VoxSubCommandKind.All;

    /// <summary>
    /// MKV 输出是否启用（embed / all）。
    /// </summary>
    public bool IsOutputVideoEnabled => SelectedCommandKind is VoxSubCommandKind.Embed or VoxSubCommandKind.All;

    /// <summary>
    /// 语言选项是否启用（embed 模式下不显示 zh-Hans，因为只用于元数据）。
    /// </summary>
    public List<string> ActiveLanguageOptions =>
        SelectedCommandKind == VoxSubCommandKind.Embed
            ? LanguageOptions.Where(l => l != "zh-Hans").ToList()
            : LanguageOptions;

    /// <summary>
    /// Whisper 参数是否可见（非 embed 模式）。
    /// </summary>
    public bool IsWhisperParamsVisible => SelectedCommandKind != VoxSubCommandKind.Embed;

    /// <summary>
    /// Embed 参数是否可见（非 transcribe 模式）。
    /// </summary>
    public bool IsEmbedParamsVisible => SelectedCommandKind != VoxSubCommandKind.Transcribe;

    // ----- 当命令类型或运行状态变化时，通知依赖属性刷新 -----
    partial void OnSelectedCommandKindChanged(VoxSubCommandKind value)
    {
        OnPropertyChanged(nameof(IsTranscribeSelected));
        OnPropertyChanged(nameof(IsEmbedSelected));
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsEmbedMode));
        OnPropertyChanged(nameof(IsOutputSrtEnabled));
        OnPropertyChanged(nameof(IsOutputVideoEnabled));
        OnPropertyChanged(nameof(ActiveLanguageOptions));
        OnPropertyChanged(nameof(IsWhisperParamsVisible));
        OnPropertyChanged(nameof(IsEmbedParamsVisible));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        RunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    // ----- 文件浏览命令 -----
    [RelayCommand]
    private async Task BrowseMedia()
    {
        var path = await BrowseFileAsync(
            L("SelectMediaFile"),
            $"{L("AllMediaFiles")}|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.mp3;*.wav;*.m4a;*.flac|{L("AllFiles")}|*.*");
        if (path is not null)
            MediaPath = path;
    }

    [RelayCommand]
    private async Task BrowseSubtitle()
    {
        var path = await BrowseFileAsync(
            L("SelectSubtitleFile"),
            $"{L("SrtSubtitles")}|*.srt|{L("AllFiles")}|*.*");
        if (path is not null)
            SubtitlePath = path;
    }

    [RelayCommand]
    private async Task BrowseOutputSrt()
    {
        var path = await SaveFileAsync(
            L("SelectSrtOutput"),
            $"{L("SrtSubtitles")}|*.srt");
        if (path is not null)
            OutputSrtPath = path;
    }

    [RelayCommand]
    private async Task BrowseOutputVideo()
    {
        var path = await SaveFileAsync(
            L("SelectMkvOutput"),
            $"{L("MkvVideos")}|*.mkv");
        if (path is not null)
            OutputVideoPath = path;
    }

    // ----- 执行命令 -----
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task Run()
    {
        // 1. 校验
        var errors = Validate();
        if (errors.Count > 0)
        {
            AppendLog(L("ErrorValidationFailed"));
            foreach (var err in errors)
                AppendLog($"  - {err}");
            StatusText = L("StatusParamError");
            return;
        }

        // 2. 预检工具链
        var settings = AppSettingsService.Load();
        var ffmpeg = ToolLocator.FindFfmpeg(settings.FfmpegPath);
        if (ffmpeg is null)
        {
            AppendLog(L("ErrorFfmpegNotFound"));
            StatusText = L("StatusToolchainMissing");
            return;
        }

        // 3. 创建任务
        var job = BuildJob();

        IsRunning = true;
        StatusText = L("StatusPreparing");
        _cts = new CancellationTokenSource();

        try
        {
            if (!await VoxSubPythonEnvironment.EnsureReadyAsync(settings, Device, AppendLog, _cts.Token))
            {
                StatusText = L("StatusEnvironmentNotReady");
                return;
            }

            var processResolution = VoxSubProcessResolver.Resolve(settings);
            if (processResolution.Spec is null)
            {
                foreach (var diagnostic in processResolution.Diagnostics)
                    AppendLog(diagnostic);

                StatusText = L("StatusToolchainMissing");
                return;
            }

            StatusText = L("StatusRunning");
            var jobArgs = VoxSubCommandBuilder.BuildArguments(job);
            var processSpec = processResolution.Spec;
            var executable = processSpec.Executable;
            var allArgs = processSpec.PrefixArguments.ToList();
            allArgs.AddRange(jobArgs);

            AppendLog($"{L("InfoUsingFfmpeg")}{ffmpeg}");
            AppendLog($"{L("InfoExecuting")}{executable} {string.Join(" ", allArgs)}");
            AppendLog("");

            var exitCode = await VoxSubRunner.RunAsync(
                executable,
                allArgs,
                line => AppendLog(line),
                line => AppendLog($"[stderr] {line}"),
                _cts.Token,
                VoxSubRunner.BuildEnvironmentOverrides(settings));

            if (exitCode == 0)
            {
                AppendLog("");
                AppendLog(L("CompletedSuccess"));
                StatusText = L("StatusSuccess");
            }
            else
            {
                AppendLog("");
                AppendLog($"{L("ErrorFailedExitCode")}{exitCode}");
                StatusText = L("StatusFailed");
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("");
            AppendLog(L("Cancelled"));
            StatusText = L("StatusCancelled");
        }
        catch (Exception ex)
        {
            AppendLog("");
            AppendLog($"{L("Exception")}{ex.Message}");
            StatusText = L("StatusError");
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private bool CanRun() => !IsRunning;

    // ----- 取消命令 -----
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsRunning;

    // ----- 清空日志 -----
    [RelayCommand]
    private void ClearLog()
    {
        LogText = string.Empty;
    }

    // ----- 内部辅助方法 -----
    private List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(MediaPath))
        {
            errors.Add(L("ErrorSelectMedia"));
        }
        else if (!File.Exists(MediaPath))
        {
            errors.Add($"{L("ErrorMediaNotExists")}{MediaPath}");
        }

        if (SelectedCommandKind == VoxSubCommandKind.Embed && !string.IsNullOrWhiteSpace(SubtitlePath))
        {
            if (!File.Exists(SubtitlePath))
                errors.Add($"{L("ErrorSubtitleNotExists")}{SubtitlePath}");
        }

        // overwrite 未勾选时检查输出文件是否已存在
        if (!Overwrite)
        {
            if (!string.IsNullOrWhiteSpace(OutputSrtPath) && File.Exists(OutputSrtPath))
                errors.Add(string.Format(L("ErrorSrtExists"), OutputSrtPath));

            if (!string.IsNullOrWhiteSpace(OutputVideoPath) && File.Exists(OutputVideoPath))
                errors.Add(string.Format(L("ErrorMkvExists"), OutputVideoPath));
        }

        return errors;
    }

    private VoxSubJob BuildJob()
    {
        return new VoxSubJob
        {
            CommandKind = SelectedCommandKind,
            MediaPath = MediaPath,
            SubtitlePath = string.IsNullOrWhiteSpace(SubtitlePath) ? null : SubtitlePath,
            OutputSrtPath = string.IsNullOrWhiteSpace(OutputSrtPath) ? null : OutputSrtPath,
            OutputVideoPath = string.IsNullOrWhiteSpace(OutputVideoPath) ? null : OutputVideoPath,
            Language = NormalizeLanguage(Language),
            Model = string.IsNullOrWhiteSpace(Model) ? null : Model,
            Device = string.IsNullOrWhiteSpace(Device) ? null : Device,
            Fp16 = string.IsNullOrWhiteSpace(Fp16) ? null : Fp16,
            Verbose = Verbose,
            Overwrite = Overwrite,
        };
    }

    private static string? NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language) ||
            string.Equals(language.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return language.Trim();
    }

    private void AppendLog(string text)
    {
        LogText += text + Environment.NewLine;
        // 触发 PropertyChanged 让 UI 自动滚动（如果需要）。
        OnPropertyChanged(nameof(LogText));
    }

    private static async Task<string?> BrowseFileAsync(string title, string filters)
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel is null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = ParseFileFilters(filters),
        });

        if (files.Count > 0)
            return files[0].Path.LocalPath;

        return null;
    }

    private static async Task<string?> SaveFileAsync(string title, string defaultFilter)
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel is null)
            return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = ParseFileFilters(defaultFilter),
        });

        return file?.Path.LocalPath;
    }

    /// <summary>
    /// 简易字符串解析："描述|*.ext;*.ext2|描述2|*.ext3" -> FilePickerFileType 列表。
    /// </summary>
    private static List<Avalonia.Platform.Storage.FilePickerFileType> ParseFileFilters(string filters)
    {
        var result = new List<Avalonia.Platform.Storage.FilePickerFileType>();
        var parts = filters.Split('|');

        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var name = parts[i];
            var patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeFilePattern)
                .Where(p => p.Length > 0)
                .ToList();

            result.Add(new Avalonia.Platform.Storage.FilePickerFileType(name)
            {
                Patterns = patterns,
            });
        }

        return result;
    }

    private static string NormalizeFilePattern(string pattern)
    {
        pattern = pattern.Trim();

        if (pattern.Length == 0 || pattern.Contains('*') || pattern.Contains('?'))
            return pattern;

        if (pattern.StartsWith('.'))
            return "*" + pattern;

        return "*." + pattern;
    }
}
