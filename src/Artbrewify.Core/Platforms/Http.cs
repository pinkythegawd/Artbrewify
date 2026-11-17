using System.Net;
using Newtonsoft.Json.Linq;

namespace Artbrewify.Core.Platforms;

internal static class Http
{
    public static HttpClient CreateClient(string userAgent, string? referer = null)
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseCookies = false
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        if (referer is not null)
            client.DefaultRequestHeaders.Referrer = new Uri(referer);
        return client;
    }

    public static async Task<JObject> GetJsonAsync(this HttpClient client, string url, CancellationToken cancellationToken)
    {
        // Cloudflare occasionally answers a cold request with a transient 403/429/5xx;
        // a short backoff retry rides through it without surfacing a false failure.
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            using var response = await client.GetAsync(url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
                return JObject.Parse(body);

            var transient = response.StatusCode is
                System.Net.HttpStatusCode.Forbidden or
                System.Net.HttpStatusCode.TooManyRequests or
                System.Net.HttpStatusCode.InternalServerError or
                System.Net.HttpStatusCode.BadGateway or
                System.Net.HttpStatusCode.ServiceUnavailable or
                System.Net.HttpStatusCode.GatewayTimeout;
            if (!transient || attempt >= attempts)
                throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase} for {url}", null, response.StatusCode);

            await Task.Delay(TimeSpan.FromMilliseconds(600 * attempt), cancellationToken);
        }
    }

    public static string NoCache(string url)
    {
        var sep = url.Contains('?') ? '&' : '?';
        var token = Guid.NewGuid().ToString("N")[..8];
        return $"{url}{sep}{token[..4]}={token[4..]}";
    }
}