using Artbrewify.Core.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;

namespace Artbrewify.Views;

public sealed partial class PixivLoginWindow : Window
{
    private readonly PixivAuth _auth = new();

    public PixivLoginWindow()
    {
        InitializeComponent();
        Title = "Log in to Pixiv";
        AppWindow.Resize(new SizeInt32(920, 780));
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        Closed += (_, _) => _auth.Dispose();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Browser.CoreWebView2.Navigate(_auth.CreateLoginUrl());
        }
        catch (Exception ex)
        {
            StatusText.Text = "WebView2 runtime unavailable. Install the Evergreen WebView2 Runtime. " + ex.Message;
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var isCallback = e.Uri.StartsWith("pixiv://", StringComparison.OrdinalIgnoreCase)
                         || e.Uri.Contains("/users/auth/pixiv/callback", StringComparison.OrdinalIgnoreCase);
        if (!isCallback) return;

        e.Cancel = true;
        var code = ExtractCode(e.Uri);
        if (code is null)
        {
            StatusText.Text = "Could not read the login code from the redirect.";
            return;
        }
        _ = CompleteLoginAsync(code);
    }

    private async Task CompleteLoginAsync(string code)
    {
        StatusText.Text = "Completing login…";
        try
        {
            var tokens = await _auth.ExchangeCodeAsync(code, CancellationToken.None);
            App.Settings.Current.PixivRefreshToken = tokens.RefreshToken;
            App.Settings.Save();
            StatusText.Text = "Logged in to Pixiv! Closing…";
            await Task.Delay(900);
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Login failed: " + ex.Message;
        }
    }

    private static string? ExtractCode(string uri)
    {
        var idx = uri.IndexOf("code=", StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + "code=".Length;
        var end = uri.IndexOf('&', start);
        var code = end < 0 ? uri[start..] : uri[start..end];
        return code.Length > 0 ? Uri.UnescapeDataString(code) : null;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}