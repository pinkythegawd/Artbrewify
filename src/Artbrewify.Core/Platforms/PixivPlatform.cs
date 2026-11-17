using Newtonsoft.Json.Linq;

namespace Artbrewify.Core.Platforms;

public sealed class PixivPlatform : IArtPlatform, IDisposable
{
    private const string ApiBase = "https://app-api.pixiv.net";
    private const string IosUa = "PixivIOSApp/7.19.1 (iOS 16.7.2; iPhone12,8)";
    private const string ImageReferer = "https://www.pixiv.net/";

    private readonly HttpClient _client = Http.CreateClient(IosUa, ApiBase + "/");
    private string? _accessToken;

    public PixivPlatform()
    {
        _client.DefaultRequestHeaders.TryAddWithoutValidation("App-OS", "ios");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("App-OS-Version", "16.7.2");
        _client.DefaultRequestHeaders.TryAddWithoutValidation("App-Version", "7.19.1");
        _client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
    }

    public string Name => "Pixiv";
    public bool RequiresLogin => true;
    public bool IsReady => _accessToken is not null;

    public void SetAccessToken(string accessToken)
    {
        _accessToken = accessToken;
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<ArtistGallery> FetchGalleryAsync(string input, IProgress<string>? status, CancellationToken cancellationToken)
    {
        if (_accessToken is null) throw new InvalidOperationException("Log in to Pixiv first");

        var userId = await ResolveUserIdAsync(input, cancellationToken);
        status?.Report($"Loading artist {userId}…");
        var artistName = await GetArtistNameAsync(userId, cancellationToken);

        var items = new List<ImageItem>();
        string? nextUrl = $"{ApiBase}/v1/user/illusts?user_id={userId}&filter=for_ios";
        while (nextUrl is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await _client.GetJsonAsync(nextUrl, cancellationToken);
            if (json["illusts"] is JArray illusts)
            {
                foreach (var illust in illusts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var title = illust["title"]?.ToString() ?? "illust";
                    status?.Report($"Reading: {title}");
                    await CollectIllustAsync(illust, items, cancellationToken);
                }
            }
            nextUrl = json["next_url"]?.ToString();
        }

        return new ArtistGallery(artistName, items);
    }

    private async Task CollectIllustAsync(JToken illust, List<ImageItem> items, CancellationToken cancellationToken)
    {
        var id = illust["id"]?.ToString() ?? "";
        var title = illust["title"]?.ToString() ?? "illust";
        var type = illust["type"]?.ToString();

        if (type == "ugoira")
        {
            await CollectUgoiraAsync(id, title, items, cancellationToken);
            return;
        }

        var pageCount = illust["page_count"]?.Value<int>() ?? 1;
        if (pageCount > 1 && illust["meta_pages"] is JArray pages)
        {
            for (var p = 0; p < pages.Count; p++)
            {
                var url = pages[p]["image_urls"]?["original"]?.ToString();
                if (url is null) continue;
                items.Add(new ImageItem
                {
                    Url = url,
                    Title = title,
                    Id = $"{id}_p{p}",
                    Extension = FileName.ExtensionFromUrl(url),
                    Referer = ImageReferer
                });
            }
        }
        else
        {
            var url = illust["meta_single_page"]?["original_image_url"]?.ToString();
            if (url is null || url.Contains("limit_")) return;
            items.Add(new ImageItem
            {
                Url = url,
                Title = title,
                Id = id,
                Extension = FileName.ExtensionFromUrl(url),
                Referer = ImageReferer
            });
        }
    }

    private async Task CollectUgoiraAsync(string id, string title, List<ImageItem> items, CancellationToken cancellationToken)
    {
        try
        {
            var json = await _client.GetJsonAsync($"{ApiBase}/v1/ugoira/metadata?illust_id={id}", cancellationToken);
            var meta = json["ugoira_metadata"];
            var zipUrl = meta?["zip_urls"]?["medium"]?.ToString();
            if (zipUrl is null) return;
            zipUrl = zipUrl.Replace("_ugoira600x600", "_ugoira1920x1080");

            var frames = new List<UgoiraFrame>();
            if (meta?["frames"] is JArray frameArray)
            {
                foreach (var f in frameArray)
                {
                    var file = f["file"]?.ToString();
                    var delay = f["delay"]?.Value<int>() ?? 100;
                    if (file is not null) frames.Add(new UgoiraFrame(file, delay));
                }
            }

            items.Add(new ImageItem
            {
                Url = zipUrl,
                Title = title,
                Id = id,
                Extension = "zip",
                Referer = ImageReferer,
                Kind = DownloadItemKind.Ugoira,
                UgoiraFrames = frames
            });
        }
        catch (HttpRequestException)
        {
            // ugoira metadata unavailable (e.g. sanity-level block); skip
        }
    }

    private async Task<string> ResolveUserIdAsync(string input, CancellationToken cancellationToken)
    {
        input = input.Trim();
        if (int.TryParse(input, out _)) return input;

        if (input.Contains("pixiv.net") && input.Contains("/users/"))
        {
            var tail = input.Split("/users/")[1];
            var id = new string(tail.TakeWhile(char.IsDigit).ToArray());
            if (id.Length > 0) return id;
        }

        var search = await _client.GetJsonAsync(
            $"{ApiBase}/v1/search/user?word={Uri.EscapeDataString(input)}&filter=for_ios", cancellationToken);
        var first = search["user_previews"]?.FirstOrDefault();
        return first?["user"]?["id"]?.ToString()
            ?? throw new InvalidOperationException($"No Pixiv user found for '{input}'");
    }

    private async Task<string> GetArtistNameAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var json = await _client.GetJsonAsync($"{ApiBase}/v1/user/detail/{userId}?filter=for_ios", cancellationToken);
            return json["user"]?["name"]?.ToString() ?? userId;
        }
        catch (HttpRequestException)
        {
            return userId;
        }
    }

    public void Dispose() => _client.Dispose();
}