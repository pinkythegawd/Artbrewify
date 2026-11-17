using Newtonsoft.Json;

namespace Artbrewify.Core;

public sealed class AppSettings
{
    public string? SavePath { get; set; }
    public int ConcurrentDownloads { get; set; } = 4;
    public int RetryAttempts { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public bool ConvertUgoiraToGif { get; set; } = true;
    public bool IncludeMatureContent { get; set; }
    public string? PixivRefreshTokenProtected { get; set; }
    public string? DeviantArtClientId { get; set; }
    public string? DeviantArtClientSecretProtected { get; set; }

    [JsonIgnore]
    public string? PixivRefreshToken
    {
        get => SecureStorage.Unprotect(PixivRefreshTokenProtected);
        set => PixivRefreshTokenProtected = string.IsNullOrEmpty(value) ? null : SecureStorage.Protect(value);
    }

    [JsonIgnore]
    public string? DeviantArtClientSecret
    {
        get => SecureStorage.Unprotect(DeviantArtClientSecretProtected);
        set => DeviantArtClientSecretProtected = string.IsNullOrEmpty(value) ? null : SecureStorage.Protect(value);
    }

    [JsonIgnore]
    public string EffectiveSavePath =>
        string.IsNullOrWhiteSpace(SavePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Artbrewify")
            : SavePath;

    public DownloadOptions ToDownloadOptions() => new()
    {
        ConcurrentDownloads = ConcurrentDownloads,
        RetryAttempts = RetryAttempts,
        TimeoutSeconds = TimeoutSeconds,
        ConvertUgoiraToGif = ConvertUgoiraToGif
    };
}

public sealed class SettingsStore
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Artbrewify");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                Current = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(Current, Formatting.Indented));
    }
}