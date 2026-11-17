using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Artbrewify.Core.Platforms;

public sealed class DeviantArtPlatform : IArtPlatform, IDisposable
{
    private const string TokenUrl = "https://www.deviantart.com/oauth2/token";
    private const string ApiBase = "https://www.deviantart.com/api/v1/oauth2";
    private const string BrowserUa =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private readonly HttpClient _client = Http.CreateClient(BrowserUa);
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly bool _matureContent;
    private string? _accessToken;

    public DeviantArtPlatform(string clientId, string clientSecret, bool matureContent)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _matureContent = matureContent;
    }

    public string Name => "DeviantArt";
    public bool RequiresLogin => true;
    public bool IsReady => _accessToken is not null;

    public async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            })
        };
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var response = await _client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JObject.Parse(body);
        if (!response.IsSuccessStatusCode || json["status"]?.ToString() == "error")
        {
            var error = json["error_description"]?.ToString() ?? json["error"]?.ToString() ?? "invalid credentials";
            throw new InvalidOperationException($"DeviantArt authentication failed: {error}");
        }
        _accessToken = json["access_token"]?.ToString()
            ?? throw new InvalidOperationException("DeviantArt returned no access token");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task<ArtistGallery> FetchGalleryAsync(string input, IProgress<string>? status, CancellationToken cancellationToken)
    {
        if (_accessToken is null) await AuthenticateAsync(cancellationToken);
        var username = ExtractUsername(input);
        status?.Report($"Loading {username}'s DeviantArt gallery…");

        var items = new List<ImageItem>();
        var offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = $"{ApiBase}/gallery/all?username={username}&limit=24&offset={offset}&mature_content={(_matureContent ? "true" : "false")}";
            var json = await _client.GetJsonAsync(url, cancellationToken);
            if (json["results"] is not JArray results || results.Count == 0) break;

            foreach (var deviation in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var title = deviation["title"]?.ToString() ?? "deviation";
                status?.Report($"Reading: {title}");
                var item = await BuildItemAsync(deviation, cancellationToken);
                if (item is not null) items.Add(item);
            }

            if (json["has_more"]?.Value<bool>() != true) break;
            offset = json["next_offset"]?.Value<int>() ?? offset + 24;
        }

        return new ArtistGallery(username, items);
    }

    private async Task<ImageItem?> BuildItemAsync(JToken deviation, CancellationToken cancellationToken)
    {
        var id = deviation["deviationid"]?.ToString();
        var title = deviation["title"]?.ToString() ?? "deviation";
        var src = deviation["content"]?["src"]?.ToString();
        if (id is null || src is null) return null;

        var downloadable = deviation["is_downloadable"]?.Value<bool>() == true;
        if (downloadable)
        {
            try
            {
                var dl = await _client.GetJsonAsync($"{ApiBase}/deviation/download/{id}", cancellationToken);
                var dlSrc = dl["src"]?.ToString();
                if (dlSrc is not null)
                {
                    var filename = dl["filename"]?.ToString();
                    return new ImageItem
                    {
                        Url = dlSrc,
                        Title = title,
                        Id = id,
                        Extension = filename is not null ? FileName.ExtensionFromUrl(filename) : FileName.ExtensionFromUrl(dlSrc),
                        FallbackUrls = new[] { src }
                    };
                }
            }
            catch (HttpRequestException)
            {
                // fall through to JWT method
            }
        }

        var (originalUrl, extension) = RewriteToOriginal(src);
        return new ImageItem
        {
            Url = originalUrl,
            Title = title,
            Id = id,
            Extension = extension,
            FallbackUrls = originalUrl != src ? new[] { src } : null
        };
    }

    // Removes width/height limits from a wixmp CDN URL by re-signing with an alg:none JWT.
    // Technique discovered by @Ironchest337, as implemented in gallery-dl.
    private static (string Url, string Extension) RewriteToOriginal(string src)
    {
        var idx = src.IndexOf("/v1/", StringComparison.Ordinal);
        if (idx < 0) return (src, FileName.ExtensionFromUrl(src));

        var baseUrl = src[..idx].Replace("//images-wixmp", "//wixmp");
        var fIdx = baseUrl.IndexOf("/f/", StringComparison.Ordinal);
        var path = fIdx >= 0 ? baseUrl[(fIdx + "/f/".Length)..] : "";
        var payload = $"{{\"sub\":\"urn:app:\",\"iss\":\"urn:app:\",\"obj\":[[{{\"path\":\"/f/{path}\"}}]],\"aud\":[\"urn:service:file.download\"]}}";
        var pl = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=');
        var url = $"{baseUrl}?token=eyJ0eXAiOiJKV1QiLCJhbGciOiJub25lIn0.{pl}.";
        return (url, FileName.ExtensionFromUrl(baseUrl));
    }

    private static string ExtractUsername(string input)
    {
        input = input.Trim();
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && uri.Host.Contains("deviantart"))
        {
            var seg = uri.AbsolutePath.Trim('/').Split('/').FirstOrDefault();
            if (!string.IsNullOrEmpty(seg)) return seg;
        }
        if (input.Contains("deviantart.com/"))
        {
            var tail = input.Split("deviantart.com/")[1];
            return tail.Trim('/').Split('/')[0].Split('?')[0];
        }
        return input.Trim('/');
    }

    public void Dispose() => _client.Dispose();
}