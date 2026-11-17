using System.Net;

namespace Artbrewify.Core;

public sealed class ArtDownloader : IDisposable
{
    private readonly HttpClient _client;

    public ArtDownloader(HttpMessageHandler? handler = null)
    {
        _client = handler is null
            ? new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            : new HttpClient(handler);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        _client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task DownloadAsync(
        ArtistGallery gallery,
        string savePath,
        DownloadOptions options,
        IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        var artistDir = Path.Combine(savePath, FileName.Sanitize(gallery.ArtistName));
        Directory.CreateDirectory(artistDir);

        var items = gallery.Items
            .Where(i => !File.Exists(FinalPath(artistDir, i, options)))
            .ToList();

        var total = gallery.Items.Count;
        var completed = total - items.Count;
        var failed = 0;
        var gate = new object();

        void Report(string? current = null, string? message = null, DownloadPhase phase = DownloadPhase.Running)
        {
            int c, f;
            lock (gate) { c = completed; f = failed; }
            progress.Report(new DownloadProgress(phase, c, total, f, current, message));
        }

        if (items.Count == 0)
        {
            progress.Report(new DownloadProgress(DownloadPhase.Completed, total, total, 0, Message: "Everything already downloaded"));
            return;
        }

        Report(message: $"Downloading {items.Count} of {total} files");
        using var semaphore = new SemaphoreSlim(Math.Max(1, options.ConcurrentDownloads));

        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var fileName = Path.GetFileName(FinalPath(artistDir, item, options));
                Report(fileName);
                await DownloadItemAsync(item, FinalPath(artistDir, item, options), options, cancellationToken);
                lock (gate) { completed++; }
                Report(fileName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                lock (gate) { failed++; }
                Report(message: $"{item.Title}: {e.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
            Report(phase: DownloadPhase.Completed);
        }
        catch (OperationCanceledException)
        {
            foreach (var t in tasks) { _ = t.Exception; }
            Report(phase: DownloadPhase.Cancelled);
        }
    }

    private static string FinalPath(string artistDir, ImageItem item, DownloadOptions options)
    {
        var ext = item.Kind == DownloadItemKind.Ugoira
            ? (options.ConvertUgoiraToGif ? "gif" : "zip")
            : item.Extension;
        return Path.Combine(artistDir, $"{FileName.Sanitize(item.Title)}_{FileName.Sanitize(item.Id)}.{ext}");
    }

    private async Task DownloadItemAsync(ImageItem item, string finalPath, DownloadOptions options, CancellationToken cancellationToken)
    {
        var urls = new List<string> { item.Url };
        if (item.FallbackUrls is not null) urls.AddRange(item.FallbackUrls);
        Exception? lastError = null;
        foreach (var url in urls)
        {
            var attempts = Math.Max(1, options.RetryAttempts);
            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await DownloadSingleAsync(url, item, finalPath, options, cancellationToken);
                    return;
                }
                catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    lastError = e;
                    break;
                }
                catch (Exception e) when (attempt < attempts && !cancellationToken.IsCancellationRequested)
                {
                    lastError = e;
                    await Task.Delay(Random.Shared.Next(500, 1500) * attempt, cancellationToken);
                }
                catch (Exception e)
                {
                    lastError = e;
                    break;
                }
            }
        }
        throw lastError ?? new InvalidOperationException("No download URL available");
    }

    private async Task DownloadSingleAsync(string url, ImageItem item, string finalPath, DownloadOptions options, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (item.Referer is not null)
            request.Headers.Referrer = new Uri(item.Referer);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)));
        using var response = await _client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        if (item.Kind == DownloadItemKind.Ugoira && options.ConvertUgoiraToGif && item.UgoiraFrames is { } frames)
        {
            using var memory = new MemoryStream();
            await response.Content.CopyToAsync(memory, cancellationToken);
            memory.Position = 0;
            var tempGif = finalPath + ".part";
            await UgoiraConverter.SaveAsync(memory, frames, tempGif, asGif: true, cancellationToken);
            File.Move(tempGif, finalPath, overwrite: true);
        }
        else
        {
            var tempPath = finalPath + ".part";
            await using (var fs = File.Create(tempPath))
            {
                await response.Content.CopyToAsync(fs, cancellationToken);
            }
            File.Move(tempPath, finalPath, overwrite: true);
        }
    }

    public void Dispose() => _client.Dispose();
}