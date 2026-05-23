using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using VoxSub.ViewModels;
using VoxSub.Views;

namespace VoxSub;

public partial class App : Application
{
    private static bool _isSettingsDialogOpen;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
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
}
