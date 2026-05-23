using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoxSub.Services;

namespace VoxSub.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private static string L(string key) => Localization.Instance[key];

    private static readonly IReadOnlyList<LanguageOption> AvailableLanguageOptions =
    [
        new(Localization.SystemCulture, "SystemLanguage"),
        new(Localization.DefaultCulture, "Chinese"),
        new(Localization.EnglishCulture, "English"),
    ];

    private static readonly IReadOnlyList<FilePickerFileType> ExecutableFileTypes =
    [
        new(L("ExecutableFiles"))
        {
            Patterns = ["*.exe", "*.cmd", "*.bat", "*.ps1"],
        },
        new(L("AllFiles"))
        {
            Patterns = ["*.*"],
        },
    ];

    private static readonly IReadOnlyList<FilePickerFileType> PythonScriptFileTypes =
    [
        new(L("PythonScripts"))
        {
            Patterns = ["*.py"],
        },
    ];

    [ObservableProperty]
    private string _ffmpegPath = string.Empty;

    [ObservableProperty]
    private string _pythonPath = string.Empty;

    [ObservableProperty]
    private string _voxSubPath = string.Empty;

    [ObservableProperty]
    private string _ffmpegPathHint = string.Empty;

    [ObservableProperty]
    private string _pythonPathHint = string.Empty;

    [ObservableProperty]
    private string _voxSubPathHint = string.Empty;

    [ObservableProperty]
    private string _ffmpegPathHintColor = "Gray";

    [ObservableProperty]
    private string _pythonPathHintColor = "Gray";

    [ObservableProperty]
    private string _voxSubPathHintColor = "Gray";

    [ObservableProperty]
    private bool _ffmpegPathValid;

    [ObservableProperty]
    private bool _pythonPathValid;

    [ObservableProperty]
    private bool _voxSubPathValid;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

    public IReadOnlyList<LanguageOption> LanguageOptions => AvailableLanguageOptions;

    public SettingsViewModel()
    {
        var settings = AppSettingsService.Load();
        FfmpegPath = settings.FfmpegPath;
        PythonPath = settings.PythonPath;
        VoxSubPath = settings.VoxSubPath;
        RefreshDetectedPaths();

        SelectedLanguage = FindLanguageOption(settings.Language);
    }

    public event Action<bool>? RequestClose;

    partial void OnFfmpegPathChanged(string value)
    {
        UpdateFfmpegPathStatus(value);
    }

    partial void OnPythonPathChanged(string value)
    {
        UpdatePythonPathStatus(value);
    }

    partial void OnVoxSubPathChanged(string value)
    {
        UpdateVoxSubPathStatus(value);
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null)
            return;

        Localization.Instance.CurrentCulture = value.Code;

        var settings = AppSettingsService.Load();
        settings.Language = value.Code;
        AppSettingsService.Save(settings);
    }

    partial void OnStatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasStatusText));
    }

    [RelayCommand]
    private async Task BrowseFfmpeg()
    {
        var path = await BrowseExecutableAsync(L("SelectFfmpeg"), ToolDefaults.FfmpegCommand);
        if (path is not null)
            FfmpegPath = path;
    }

    [RelayCommand]
    private async Task BrowsePython()
    {
        var path = await BrowseExecutableAsync(L("SelectPython"), ToolDefaults.PythonCommand);
        if (path is not null)
            PythonPath = path;
    }

    [RelayCommand]
    private async Task BrowseVoxSub()
    {
        var path = await BrowseExecutableAsync(L("SelectVoxSub"), ToolDefaults.VoxSubCommand, PythonScriptFileTypes);
        if (path is not null)
            VoxSubPath = path;
    }

    [RelayCommand]
    private void ResetFfmpeg()
    {
        FfmpegPath = ToolDefaults.FfmpegCommand;
    }

    [RelayCommand]
    private void ResetPython()
    {
        PythonPath = ToolDefaults.PythonCommand;
    }

    [RelayCommand]
    private void ResetVoxSub()
    {
        VoxSubPath = ToolDefaults.VoxSubCommand;
    }

    [RelayCommand]
    private void RefreshDetectedPaths()
    {
        UpdateFfmpegPathStatus(FfmpegPath);
        UpdatePythonPathStatus(PythonPath);
        UpdateVoxSubPathStatus(VoxSubPath);
    }

    [RelayCommand]
    private void Apply()
    {
        TrySaveSettings();
    }

    [RelayCommand]
    private void Save()
    {
        if (TrySaveSettings())
            RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    private void UpdateFfmpegPathStatus(string path)
    {
        FfmpegPathHint = ToolPathResolver.GetPathHint(path);
        FfmpegPathValid = ToolPathResolver.IsPathValid(path);
        FfmpegPathHintColor = FfmpegPathValid ? "Gray" : "Red";
    }

    private void UpdatePythonPathStatus(string path)
    {
        PythonPathHint = ToolPathResolver.GetPathHint(path);
        PythonPathValid = ToolPathResolver.IsPathValid(path);
        PythonPathHintColor = PythonPathValid ? "Gray" : "Red";
    }

    private void UpdateVoxSubPathStatus(string path)
    {
        VoxSubPathHint = ToolPathResolver.GetPathHint(path);
        VoxSubPathValid = ToolPathResolver.IsPathValid(path);
        VoxSubPathHintColor = VoxSubPathValid ? "Gray" : "Red";
    }

    private bool TrySaveSettings()
    {
        var settings = AppSettingsService.Load();
        settings.FfmpegPath = FfmpegPath.Trim();
        settings.PythonPath = PythonPath.Trim();
        settings.VoxSubPath = VoxSubPath.Trim();
        settings.Language = SelectedLanguage?.Code ?? Localization.DefaultCulture;

        try
        {
            AppSettingsService.Save(settings);
            StatusText = L("SettingsSaved");
            return true;
        }
        catch (Exception ex)
        {
            StatusText = $"{L("SettingsSaveFailed")}{ex.Message}";
            return false;
        }
    }

    private static async Task<string?> BrowseExecutableAsync(
        string title,
        string preferredExecutableName,
        IReadOnlyList<FilePickerFileType>? extraFileTypes = null)
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel is null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(preferredExecutableName)
                {
                    Patterns = [preferredExecutableName],
                },
                .. (extraFileTypes ?? []),
                .. ExecutableFileTypes,
            ],
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private LanguageOption FindLanguageOption(string? code)
    {
        return LanguageOptions.FirstOrDefault(option =>
                   string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase))
               ?? LanguageOptions.First(option => option.Code == Localization.DefaultCulture);
    }
}

public sealed class LanguageOption : INotifyPropertyChanged
{
    public string Code { get; }
    public string DisplayKey { get; }
    public string DisplayName => Localization.Instance[DisplayKey];

    public LanguageOption(string code, string displayKey)
    {
        Code = code;
        DisplayKey = displayKey;
        Localization.Instance.PropertyChanged += OnLocalizationChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => DisplayName;

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Localization.CurrentCulture) or nameof(Localization.Strings))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }
}
