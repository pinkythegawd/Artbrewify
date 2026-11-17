using System.Diagnostics;
using System.Net;

namespace Artbrewify.Core.Platforms;

// ArtStation's JSON API sits behind Cloudflare bot management that fingerprints the
// TLS ClientHello. Every .NET TLS stack (SocketsHttpHandler/Schannel and WinHTTP) is
// challenged with a 403, even with a valid __cf_bm cookie, while the system curl.exe
// handshake passes. We therefore shell out to curl.exe for the JSON endpoints only;
// image assets on the CDN are not fingerprinted and download fine over HttpClient.
internal sealed class CurlJson : IDisposable
{
    private readonly string _curlPath;
    private readonly string _jarPath;
    private readonly string _userAgent;

    public static bool IsAvailable => File.Exists(CurlPath);
    private static string CurlPath => Path.Combine(Environment.SystemDirectory, "curl.exe");

    public CurlJson(string userAgent)
    {
        _curlPath = CurlPath;
        _userAgent = userAgent;
        _jarPath = Path.Combine(Path.GetTempPath(), $"artbrewify-cf-{Guid.NewGuid():N}.txt");
    }

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        const int attempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var (status, body) = await RunOnceAsync(url, cancellationToken);
            if (status == 200 && LooksLikeJson(body))
                return body;

            var transient = status is 403 or 429 or 500 or 502 or 503 or 504;
            if (!transient || attempt >= attempts)
                throw new HttpRequestException($"{status} for {url}", null, (HttpStatusCode)status);

            await Task.Delay(TimeSpan.FromMilliseconds(600 * attempt), cancellationToken);
        }
    }

    private static bool LooksLikeJson(string body)
    {
        var t = body.TrimStart();
        return t.StartsWith('{') || t.StartsWith('[');
    }

    private async Task<(int Status, string Body)> RunOnceAsync(string url, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _curlPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add("--compressed");
        psi.ArgumentList.Add("--max-time");
        psi.ArgumentList.Add("60");
        psi.ArgumentList.Add("-A");
        psi.ArgumentList.Add(_userAgent);
        psi.ArgumentList.Add("-H");
        psi.ArgumentList.Add("Accept: application/json, text/plain, */*");
        psi.ArgumentList.Add("-H");
        psi.ArgumentList.Add("Referer: https://www.artstation.com/");
        psi.ArgumentList.Add("-b");
        psi.ArgumentList.Add(_jarPath);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(_jarPath);
        psi.ArgumentList.Add("-w");
        psi.ArgumentList.Add("\n%{http_code}");
        psi.ArgumentList.Add(url);

        using var process = new Process { StartInfo = psi };
        process.Start();
        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await stdoutTask;
        _ = await stderrTask;

        var idx = output.LastIndexOf('\n');
        if (idx < 0 || !int.TryParse(output[(idx + 1)..].Trim(), out var status))
            return (0, output);
        return (status, output[..idx]);
    }

    public void Dispose()
    {
        try { if (File.Exists(_jarPath)) File.Delete(_jarPath); } catch { }
    }
}