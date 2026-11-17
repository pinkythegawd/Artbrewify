using Artbrewify.Core.Platforms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Artbrewify.Views;

public sealed partial class SettingsPage : Page
{
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        var s = App.Settings.Current;
        ConcurrentBox.Value = s.ConcurrentDownloads;
        RetryBox.Value = s.RetryAttempts;
        TimeoutBox.Value = s.TimeoutSeconds;
        UgoiraToggle.IsOn = s.ConvertUgoiraToGif;
        MatureToggle.IsOn = s.IncludeMatureContent;
        DaClientIdBox.Text = s.DeviantArtClientId ?? "";
        DaClientSecretBox.Password = s.DeviantArtClientSecret ?? "";
        RefreshPixivStatus();
        _loading = false;
    }

    private void ConcurrentBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        App.Settings.Current.ConcurrentDownloads = (int)args.NewValue;
        App.Settings.Save();
    }

    private void RetryBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        App.Settings.Current.RetryAttempts = (int)args.NewValue;
        App.Settings.Save();
    }

    private void TimeoutBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        App.Settings.Current.TimeoutSeconds = (int)args.NewValue;
        App.Settings.Save();
    }

    private void UgoiraToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Current.ConvertUgoiraToGif = UgoiraToggle.IsOn;
        App.Settings.Save();
    }

    private void MatureToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        App.Settings.Current.IncludeMatureContent = MatureToggle.IsOn;
        App.Settings.Save();
    }

    private void RefreshPixivStatus()
    {
        var loggedIn = !string.IsNullOrEmpty(App.Settings.Current.PixivRefreshToken);
        PixivStatusText.Text = loggedIn ? "Logged in to Pixiv." : "Not logged in.";
        PixivLogoutButton.IsEnabled = loggedIn;
    }

    private void PixivLoginButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new PixivLoginWindow();
        window.Closed += (_, _) => RefreshPixivStatus();
        window.Activate();
    }

    private void PixivLogoutButton_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.Current.PixivRefreshToken = null;
        App.Settings.Save();
        RefreshPixivStatus();
        ShowMessage(InfoBarSeverity.Informational, "Logged out of Pixiv.");
    }

    private async void DaSaveButton_Click(object sender, RoutedEventArgs e)
    {
        var id = DaClientIdBox.Text.Trim();
        var secret = DaClientSecretBox.Password.Trim();
        if (id.Length == 0 || secret.Length == 0)
        {
            ShowMessage(InfoBarSeverity.Warning, "Enter both client ID and client secret.");
            return;
        }

        App.Settings.Current.DeviantArtClientId = id;
        App.Settings.Current.DeviantArtClientSecret = secret;
        App.Settings.Save();

        DaSaveButton.IsEnabled = false;
        DaStatusText.Text = "Testing…";
        try
        {
            using var platform = new DeviantArtPlatform(id, secret, App.Settings.Current.IncludeMatureContent);
            await platform.AuthenticateAsync(CancellationToken.None);
            DaStatusText.Text = "Credentials valid.";
            ShowMessage(InfoBarSeverity.Success, "DeviantArt credentials saved and verified.");
        }
        catch (Exception ex)
        {
            DaStatusText.Text = "Test failed.";
            ShowMessage(InfoBarSeverity.Error, ex.Message);
        }
        finally
        {
            DaSaveButton.IsEnabled = true;
        }
    }

    private void ShowMessage(InfoBarSeverity severity, string message)
    {
        SettingsMessageBar.Severity = severity;
        SettingsMessageBar.Message = message;
        SettingsMessageBar.IsOpen = true;
    }
}