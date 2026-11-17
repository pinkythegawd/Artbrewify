using Artbrewify.Core;
using Artbrewify.Core.Platforms;
using Artbrewify.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;

namespace Artbrewify.Views;

public sealed partial class DownloadPage : Page
{
    private string _selectedPlatform = PlatformFactory.ArtStation;
    private ArtistGallery? _gallery;
    private CancellationTokenSource? _cts;
    private bool _busy;

    public DownloadPage()
    {
        InitializeComponent();
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
        if (File.Exists(logoPath))
            LogoImage.Source = new BitmapImage(new Uri(logoPath));
        FolderPathText.Text = App.Settings.Current.EffectiveSavePath;
    }

    private void DownloadPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (PlatformSelector.SelectedItem is null)
            PlatformSelector.SelectedItem = ArtStationItem;
        UpdateLoginHint();
        RefreshButtons();
    }

    private void PlatformSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is not SelectorBarItem item) return;
        _selectedPlatform = item.Tag as string ?? PlatformFactory.ArtStation;
        ArtistInput.PlaceholderText = _selectedPlatform switch
        {
            PlatformFactory.Pixiv => "e.g. https://www.pixiv.net/en/users/12345 or artist name",
            PlatformFactory.DeviantArt => "e.g. https://www.deviantart.com/username",
            _ => "e.g. https://www.artstation.com/username"
        };
        UpdateLoginHint();
        ResetPreview();
        RefreshButtons();
    }

    private void UpdateLoginHint()
    {
        var s = App.Settings.Current;
        LoginHint.Text = _selectedPlatform switch
        {
            PlatformFactory.Pixiv => string.IsNullOrEmpty(s.PixivRefreshToken)
                ? "Pixiv needs a login — set it up in Settings."
                : "Logged in to Pixiv.",
            PlatformFactory.DeviantArt => string.IsNullOrEmpty(s.DeviantArtClientId) || string.IsNullOrEmpty(s.DeviantArtClientSecret)
                ? "DeviantArt needs your own API app credentials — add them in Settings."
                : "DeviantArt API credentials configured.",
            _ => "Public galleries — no login needed."
        };
    }

    private void ArtistInput_TextChanged(object sender, TextChangedEventArgs e) => RefreshButtons();

    private void RefreshButtons()
    {
        FetchButton.IsEnabled = !_busy && !string.IsNullOrWhiteSpace(ArtistInput.Text);
        DownloadButton.IsEnabled = !_busy && _gallery is not null && _gallery.Items.Count > 0;
        CancelButton.IsEnabled = _busy;
    }

    private void ResetPreview()
    {
        _gallery = null;
        SummaryPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;
        MessageBar.IsOpen = false;
    }

    private async void FolderButton_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null) return;
        App.Settings.Current.SavePath = folder.Path;
        App.Settings.Save();
        FolderPathText.Text = folder.Path;
    }

    private async void FetchButton_Click(object sender, RoutedEventArgs e)
    {
        ResetPreview();
        _busy = true;
        RefreshButtons();
        ProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.IsIndeterminate = true;
        StatusText.Text = "Preparing…";
        _cts = new CancellationTokenSource();

        IArtPlatform? platform = null;
        try
        {
            platform = await PlatformFactory.CreateReadyAsync(_selectedPlatform, App.Settings.Current, _cts.Token);
            var status = new Progress<string>(msg => StatusText.Text = msg);
            _gallery = await platform.FetchGalleryAsync(ArtistInput.Text.Trim(), status, _cts.Token);

            PopulatePreview(_gallery);
            ShowMessage(InfoBarSeverity.Success, $"Found {_gallery.Items.Count} files for {_gallery.ArtistName}.");
        }
        catch (OperationCanceledException)
        {
            ShowMessage(InfoBarSeverity.Informational, "Fetch cancelled.");
        }
        catch (Exception ex)
        {
            ShowMessage(InfoBarSeverity.Error, ex.Message);
        }
        finally
        {
            (platform as IDisposable)?.Dispose();
            DownloadProgressBar.IsIndeterminate = false;
            _busy = false;
            RefreshButtons();
        }
    }

    private void PopulatePreview(ArtistGallery gallery)
    {
        var images = gallery.Items.Count(i => i.Kind == DownloadItemKind.Image);
        var ugoira = gallery.Items.Count(i => i.Kind == DownloadItemKind.Ugoira);
        ArtistNameText.Text = gallery.ArtistName;
        ImageCountText.Text = images.ToString();
        UgoiraCountText.Text = ugoira.ToString();
        TotalCountText.Text = gallery.Items.Count.ToString();
        ItemsList.ItemsSource = gallery.Items.Select(i => $"{i.Title}  ·  {i.Id}").ToList();
        SummaryPanel.Visibility = Visibility.Visible;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_gallery is null) return;
        _busy = true;
        RefreshButtons();
        ProgressPanel.Visibility = Visibility.Visible;
        DownloadProgressBar.IsIndeterminate = false;
        DownloadProgressBar.Value = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<DownloadProgress>(UpdateProgress);
            using var downloader = new ArtDownloader();
            await downloader.DownloadAsync(
                _gallery,
                App.Settings.Current.EffectiveSavePath,
                App.Settings.Current.ToDownloadOptions(),
                progress,
                _cts.Token);
        }
        catch (OperationCanceledException)
        {
            ShowMessage(InfoBarSeverity.Informational, "Download cancelled.");
        }
        catch (Exception ex)
        {
            ShowMessage(InfoBarSeverity.Error, ex.Message);
        }
        finally
        {
            _busy = false;
            RefreshButtons();
        }
    }

    private void UpdateProgress(DownloadProgress p)
    {
        DownloadProgressBar.Maximum = Math.Max(1, p.Total);
        DownloadProgressBar.Value = p.Completed + p.Failed;
        CompletedText.Text = $"{p.Completed} completed";
        FailedText.Text = $"{p.Failed} failed";
        CurrentFileText.Text = p.CurrentFile ?? "";
        StatusText.Text = p.Phase switch
        {
            DownloadPhase.Completed => $"Done — {p.Completed} of {p.Total} saved.",
            DownloadPhase.Cancelled => "Cancelled.",
            DownloadPhase.Failed => p.Message ?? "Failed.",
            _ => p.Message ?? "Downloading…"
        };
        if (p.Phase == DownloadPhase.Completed)
            ShowMessage(p.Failed > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success,
                $"Finished: {p.Completed} saved, {p.Failed} failed.");
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void ShowMessage(InfoBarSeverity severity, string message)
    {
        MessageBar.Severity = severity;
        MessageBar.Message = message;
        MessageBar.IsOpen = true;
    }
}