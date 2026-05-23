using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace VoxSub.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetPlatformIcon();
    }

    private void SetPlatformIcon()
    {
        try
        {
            var iconPath = GetPlatformIconPath();
            if (File.Exists(iconPath))
            {
                Icon = new WindowIcon(iconPath);
            }
        }
        catch
        {
            // 静默失败，使用默认图标
        }
    }

    private static string GetPlatformIconPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var assetName = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "voxsub-icon.icns"
            : "voxsub-icon.ico";
        return Path.Combine(baseDir, "Assets", assetName);
    }

    private async void OpenSettings_Click(object? sender, RoutedEventArgs e)
    {
        await OpenSettingsAsync();
    }

    private async Task OpenSettingsAsync()
    {
        await App.ShowSettingsDialogAsync(this);
    }

    private async void OpenAbout_Click(object? sender, RoutedEventArgs e)
    {
        await OpenAboutAsync();
    }

    private async Task OpenAboutAsync()
    {
        var aboutWindow = new AboutWindow();
        await aboutWindow.ShowDialog(this);
    }

    private void LogTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (FollowLogCheckBox.IsChecked != true)
            return;

        Dispatcher.UIThread.Post(
            () =>
            {
                LogScrollViewer.Offset = LogScrollViewer.Offset.WithY(LogScrollViewer.Extent.Height);
                LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
            },
            DispatcherPriority.Background);
    }
}
