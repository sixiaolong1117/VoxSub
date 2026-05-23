using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace VoxSub.Views;

public partial class AboutWindow : Window
{
    private const string AvatarUrl = "https://avatars.githubusercontent.com/u/59590732";
    private const string RepositoryUrl = "https://github.com/sixiaolong1117/VoxSub";
    private const string LicenseUrl = "https://github.com/sixiaolong1117/VoxSub/raw/refs/heads/master/LICENSE";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public AboutWindow()
    {
        InitializeComponent();
        LoadAvatarAsync();
        LoadVersion();
    }

    private void LoadVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}" : "v1.0.0.0";
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VoxSub", "1.0"));
        return httpClient;
    }

    private async void LoadAvatarAsync()
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(AvatarUrl);
            await using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            await Dispatcher.UIThread.InvokeAsync(() => AvatarImage.Source = bitmap);
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() => AvatarImage.Source = null);
        }
    }

    private void OpenRepository_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl(RepositoryUrl);
    }

    private void OpenLicense_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl(LicenseUrl);
    }

    private static void OpenUrl(string url)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(startInfo);
    }
}
