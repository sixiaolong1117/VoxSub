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
    private string _language = string.Empty;

    public List<string> LanguageOptions { get; } = ["", "zh", "zh-Hans", "en", "ja", "ko", "fr", "de", "es"];

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
    private string _statusText = "空闲";

    [ObservableProperty]
    private bool _isRunning;

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
        var path = await BrowseFileAsync("选择媒体文件", "所有媒体文件|*.mp4;*.mkv;*.avi;*.mov;*.wmv;*.flv;*.webm;*.mp3;*.wav;*.m4a;*.flac|所有文件|*.*");
        if (path is not null)
            MediaPath = path;
    }

    [RelayCommand]
    private async Task BrowseSubtitle()
    {
        var path = await BrowseFileAsync("选择字幕文件", "SRT 字幕|*.srt|所有文件|*.*");
        if (path is not null)
            SubtitlePath = path;
    }

    [RelayCommand]
    private async Task BrowseOutputSrt()
    {
        var path = await SaveFileAsync("选择 SRT 输出路径", "SRT 字幕|*.srt");
        if (path is not null)
            OutputSrtPath = path;
    }

    [RelayCommand]
    private async Task BrowseOutputVideo()
    {
        var path = await SaveFileAsync("选择 MKV 输出路径", "MKV 视频|*.mkv");
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
            AppendLog("[错误] 参数校验失败：");
            foreach (var err in errors)
                AppendLog($"  - {err}");
            StatusText = "参数错误";
            return;
        }

        // 2. 预检工具链
        var settings = AppSettingsService.Load();
        var ffmpeg = ToolLocator.FindFfmpeg(settings.FfmpegPath);
        if (ffmpeg is null)
        {
            AppendLog("[错误] 未找到 ffmpeg。请在设置中选择 ffmpeg 可执行文件，或将 ffmpeg 加入 PATH。");
            StatusText = "工具链缺失";
            return;
        }

        // 3. 创建任务
        var job = BuildJob();

        IsRunning = true;
        StatusText = "准备环境…";
        _cts = new CancellationTokenSource();

        try
        {
            if (!await VoxSubPythonEnvironment.EnsureReadyAsync(settings, AppendLog, _cts.Token))
            {
                StatusText = "环境未就绪";
                return;
            }

            var processResolution = VoxSubProcessResolver.Resolve(settings);
            if (processResolution.Spec is null)
            {
                foreach (var diagnostic in processResolution.Diagnostics)
                    AppendLog(diagnostic);

                StatusText = "工具链缺失";
                return;
            }

            StatusText = "运行中…";
            var jobArgs = VoxSubCommandBuilder.BuildArguments(job);
            var processSpec = processResolution.Spec;
            var executable = processSpec.Executable;
            var allArgs = processSpec.PrefixArguments.ToList();
            allArgs.AddRange(jobArgs);

            AppendLog($"[信息] 使用 ffmpeg：{ffmpeg}");
            AppendLog($"[信息] 执行命令：{executable} {string.Join(" ", allArgs)}");
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
                AppendLog("[完成] 任务成功。");
                StatusText = "成功";
            }
            else
            {
                AppendLog("");
                AppendLog($"[错误] 任务失败，退出码：{exitCode}");
                StatusText = "失败";
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("");
            AppendLog("[取消] 任务已被用户取消。");
            StatusText = "已取消";
        }
        catch (Exception ex)
        {
            AppendLog("");
            AppendLog($"[异常] {ex.Message}");
            StatusText = "异常";
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
            errors.Add("请选择媒体文件。");
        }
        else if (!File.Exists(MediaPath))
        {
            errors.Add($"媒体文件不存在：{MediaPath}");
        }

        if (SelectedCommandKind == VoxSubCommandKind.Embed && !string.IsNullOrWhiteSpace(SubtitlePath))
        {
            if (!File.Exists(SubtitlePath))
                errors.Add($"字幕文件不存在：{SubtitlePath}");
        }

        // overtwrite 未勾选时检查输出文件是否已存在
        if (!Overwrite)
        {
            if (!string.IsNullOrWhiteSpace(OutputSrtPath) && File.Exists(OutputSrtPath))
                errors.Add($"SRT 输出文件已存在：{OutputSrtPath}（请勾选'覆盖已有文件'或更改输出路径）");

            if (!string.IsNullOrWhiteSpace(OutputVideoPath) && File.Exists(OutputVideoPath))
                errors.Add($"MKV 输出文件已存在：{OutputVideoPath}（请勾选'覆盖已有文件'或更改输出路径）");
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
            Language = string.IsNullOrWhiteSpace(Language) ? null : Language,
            Model = string.IsNullOrWhiteSpace(Model) ? null : Model,
            Device = string.IsNullOrWhiteSpace(Device) ? null : Device,
            Fp16 = string.IsNullOrWhiteSpace(Fp16) ? null : Fp16,
            Verbose = Verbose,
            Overwrite = Overwrite,
        };
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
