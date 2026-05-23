using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using VoxSub.Services;
using VoxSub.ViewModels;
using VoxSub.Views;

namespace VoxSub;

public partial class App : Application
{
    private static bool _isSettingsDialogOpen;
    private NativeMenuItem? _aboutMenuItem;
    private NativeMenuItem? _settingsMenuItem;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Load settings and set initial language
        var settings = AppSettingsService.Load();
        Localization.Instance.CurrentCulture = settings.Language;

        // Listen for language changes to update NativeMenu etc.
        Localization.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Localization.CurrentCulture))
            {
                var newLang = Localization.Instance.CurrentCulture;
                var latestSettings = AppSettingsService.Load();
                latestSettings.Language = newLang;
                AppSettingsService.Save(latestSettings);
                UpdateNativeMenuText();
            }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();

        // Update menu after framework is initialized
        UpdateNativeMenuText();
    }

    public static async Task ShowSettingsDialogAsync(Window owner)
    {
        if (_isSettingsDialogOpen)
            return;

        _isSettingsDialogOpen = true;
        try
        {
            var settingsWindow = new SettingsWindow();
            await settingsWindow.ShowDialog<bool?>(owner);
        }
        finally
        {
            _isSettingsDialogOpen = false;
        }
    }

    private async void OpenSettings_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
            await ShowSettingsDialogAsync(mainWindow);
    }

    private async void OpenAbout_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
        {
            var aboutWindow = new AboutWindow();
            await aboutWindow.ShowDialog(mainWindow);
        }
    }

    private void UpdateNativeMenuText()
    {
        var loc = Localization.Instance;
        var menu = NativeMenu.GetMenu(this);
        if (menu is null)
            return;

        if (_aboutMenuItem is null && menu.Items.Count > 0)
            _aboutMenuItem = menu.Items[0] as NativeMenuItem;

        if (_settingsMenuItem is null && menu.Items.Count > 1)
            _settingsMenuItem = menu.Items[1] as NativeMenuItem;

        if (_aboutMenuItem is not null)
            _aboutMenuItem.Header = loc["AboutTitle"];

        if (_settingsMenuItem is not null)
            _settingsMenuItem.Header = loc["Settings"] + "...";
    }
}
