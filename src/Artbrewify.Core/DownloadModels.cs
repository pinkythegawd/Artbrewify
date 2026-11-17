namespace Artbrewify.Core;

public enum DownloadItemKind
{
    Image,
    Ugoira
}

public sealed record ImageItem
{
    public required string Url { get; init; }
    public required string Title { get; init; }
    public required string Id { get; init; }
    public required string Extension { get; init; }
    public string? Referer { get; init; }
    public IReadOnlyList<string>? FallbackUrls { get; init; }
    public DownloadItemKind Kind { get; init; } = DownloadItemKind.Image;
    public IReadOnlyList<UgoiraFrame>? UgoiraFrames { get; init; }
}

public sealed record UgoiraFrame(string FileName, int DelayMs);

public sealed record ArtistGallery(string ArtistName, IReadOnlyList<ImageItem> Items);

public enum DownloadPhase
{
    Preparing,
    Running,
    Completed,
    Cancelled,
    Failed
}

public sealed record DownloadProgress(
    DownloadPhase Phase,
    int Completed,
    int Total,
    int Failed,
    string? CurrentFile = null,
    string? Message = null);

public sealed class DownloadOptions
{
    public int ConcurrentDownloads { get; set; } = 4;
    public int RetryAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public bool ConvertUgoiraToGif { get; set; } = true;
}