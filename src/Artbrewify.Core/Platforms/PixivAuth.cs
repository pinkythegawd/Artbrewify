using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Artbrewify.Core.Platforms;

public sealed record PixivTokens(string AccessToken, string RefreshToken, int ExpiresIn);

public sealed class PixivAuth : IDisposable
{
    private const string ClientId = "MOBrBDS8blbauoSck0ZfDbtuzpyT";
    private const string ClientSecret = "lsACyCD94FhDUtGTXi3QzcFE2uU1hqtDaKeqrdwj";
    private const string HashSalt = "28c1fdd170a5204386cb1313c7077b34f83e4aaf4aa829ce78c231e05b0bae2c";
    private const string AuthUrl = "https://oauth.secure.pixiv.net/auth/token";
    private const string LoginUrl = "https://app-api.pixiv.net/web/v1/login";
    private const string RedirectUri = "https://app-api.pixiv.net/web/v1/users/auth/pixiv/callback";
    private const string AuthUa = "PixivAndroidApp/5.0.234 (Android 11; Pixel 5)";

    private readonly HttpClient _client = Http.CreateClient(AuthUa);
    private string? _codeVerifier;

    public string CreateLoginUrl()
    {
        _codeVerifier = UrlSafeToken(32);
        var challenge = S256(_codeVerifier);
        return $"{LoginUrl}?code_challenge={challenge}&code_challenge_method=S256&client=pixiv-android";
    }

    public async Task<PixivTokens> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        if (_codeVerifier is null) throw new InvalidOperationException("Call CreateLoginUrl first");
        var data = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"] = "authorization_code",
            ["include_policy"] = "true",
            ["code"] = code,
            ["code_verifier"] = _codeVerifier,
            ["redirect_uri"] = RedirectUri
        };
        return await PostTokenAsync(data, cancellationToken);
    }

    public async Task<PixivTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"] = "refresh_token",
            ["get_secure_url"] = "1",
            ["include_policy"] = "true",
            ["refresh_token"] = refreshToken
        };
        return await PostTokenAsync(data, cancellationToken);
    }

    private async Task<PixivTokens> PostTokenAsync(Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var clientTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss") + "+00:00";
        using var request = new HttpRequestMessage(HttpMethod.Post, AuthUrl)
        {
            Content = new FormUrlEncodedContent(data)
        };
        request.Headers.TryAddWithoutValidation("X-Client-Time", clientTime);
        request.Headers.TryAddWithoutValidation("X-Client-Hash", Md5Hex(clientTime + HashSalt));

        using var response = await _client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JObject.Parse(body);
        var root = json["response"] as JObject ?? json;
        var access = root["access_token"]?.ToString();
        var refresh = root["refresh_token"]?.ToString();
        if (access is null || refresh is null)
        {
            var error = json["error"]?.ToString() ?? json["has_error"]?.ToString() ?? "authentication failed";
            throw new InvalidOperationException($"Pixiv login failed: {error}");
        }
        var expires = root["expires_in"]?.Value<int>() ?? 3600;
        return new PixivTokens(access, refresh, expires);
    }

    private static string Md5Hex(string input)
    {
        var bytes = MD5.HashData(Encoding.ASCII.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string S256(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string UrlSafeToken(int bytes)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(buffer).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public void Dispose() => _client.Dispose();
}