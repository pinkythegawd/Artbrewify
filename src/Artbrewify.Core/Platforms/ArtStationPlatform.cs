using Newtonsoft.Json.Linq;

namespace Artbrewify.Core.Platforms;

public sealed class ArtStationPlatform : IArtPlatform, IDisposable
{
    private const string BrowserUa =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private readonly HttpClient _client = CreateClient();
    private readonly CurlJson? _curl = CurlJson.IsAvailable ? new CurlJson(BrowserUa) : null;

    private static HttpClient CreateClient()
    {
        var client = Http.CreateClient(BrowserUa, "https://www.artstation.com/");
        // Match the site's own XHR requests so Cloudflare treats us as a browser.
        client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://www.artstation.com");
        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
        return client;
    }

    private async Task<JObject> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        if (_curl is not null)
            return JObject.Parse(await _curl.GetStringAsync(url, cancellationToken));
        return await _client.GetJsonAsync(url, cancellationToken);
    }

    public string Name => "ArtStation";
    public bool RequiresLogin => false;
    public bool IsReady => true;

    public async Task<ArtistGallery> FetchGalleryAsync(string input, IProgress<string>? status, CancellationToken cancellationToken)
    {
        var username = ExtractUsername(input);
        status?.Report($"Looking up {username} on ArtStation…");

        var profile = await GetJsonAsync(
            $"https://www.artstation.com/users/{username.ToLowerInvariant()}/quick.json", cancellationToken);
        var canonical = profile["username"]?.ToString() ?? username;

        var items = new List<ImageItem>();
        var page = 1;
        int totalCount;
        do
        {
            var url = $"https://www.artstation.com/users/{canonical}/projects.json?album_id=all&page={page}";
            var json = await GetJsonAsync(url, cancellationToken);
            totalCount = json["total_count"]?.Value<int>() ?? 0;
            var data = json["data"] as JArray ?? new JArray();
            if (data.Count == 0) break;

            foreach (var project in data)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var hash = project["hash_id"]?.ToString();
                var title = project["title"]?.ToString() ?? "project";
                if (hash is null) continue;
                status?.Report($"Reading project: {title}");
                await CollectAssetsAsync(hash, title, items, cancellationToken);
            }
            page++;
        } while ((page - 1) * 50 < totalCount);

        return new ArtistGallery(canonical, items);
    }

    private async Task CollectAssetsAsync(string hash, string title, List<ImageItem> items, CancellationToken cancellationToken)
    {
        JObject json;
        try
        {
            json = await GetJsonAsync($"https://www.artstation.com/projects/{hash}.json", cancellationToken);
        }
        catch (HttpRequestException)
        {
            return;
        }
        if (json["assets"] is not JArray assets) return;

        foreach (var asset in assets)
        {
            if (asset["has_image"]?.Value<bool>() != true) continue;
            var imageUrl = asset["image_url"]?.ToString();
            var id = asset["id"]?.ToString();
            if (imageUrl is null || id is null) continue;

            var url = Http.NoCache(imageUrl);
            var fallbacks = new List<string>();
            if (url.Contains("/images/images/"))
            {
                var idx = url.IndexOf("/large/", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var lhs = url[..idx];
                    var rhs = url[(idx + "/large/".Length)..];
                    url = $"{lhs}/8k/{rhs}";
                    fallbacks.Add($"{lhs}/4k/{rhs}");
                    fallbacks.Add($"{lhs}/large/{rhs}");
                    fallbacks.Add($"{lhs}/medium/{rhs}");
                    fallbacks.Add($"{lhs}/small/{rhs}");
                }
            }

            items.Add(new ImageItem
            {
                Url = url,
                Title = title,
                Id = id,
                Extension = FileName.ExtensionFromUrl(imageUrl),
                Referer = "https://www.artstation.com/",
                FallbackUrls = fallbacks.Count > 0 ? fallbacks : null
            });
        }
    }

    private static string ExtractUsername(string input)
    {
        input = input.Trim();
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && uri.Host.Contains("artstation"))
        {
            var seg = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
            if (!string.IsNullOrEmpty(seg)) return seg;
        }
        if (input.Contains("artstation.com/"))
        {
            var tail = input.Split("artstation.com/")[1];
            return tail.Trim('/').Split('/')[0].Split('?')[0];
        }
        return input.Trim('/');
    }

    public void Dispose()
    {
        _curl?.Dispose();
        _client.Dispose();
    }
}