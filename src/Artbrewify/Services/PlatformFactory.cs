using Artbrewify.Core;
using Artbrewify.Core.Platforms;

namespace Artbrewify.Services;

public static class PlatformFactory
{
    public const string ArtStation = "artstation";
    public const string Pixiv = "pixiv";
    public const string DeviantArt = "deviantart";

    public static async Task<IArtPlatform> CreateReadyAsync(string platform, AppSettings settings, CancellationToken cancellationToken)
    {
        switch (platform)
        {
            case ArtStation:
                return new ArtStationPlatform();

            case Pixiv:
            {
                var refreshToken = settings.PixivRefreshToken;
                if (string.IsNullOrEmpty(refreshToken))
                    throw new InvalidOperationException("Log in to Pixiv in Settings first.");
                using var auth = new PixivAuth();
                var tokens = await auth.RefreshAsync(refreshToken, cancellationToken);
                settings.PixivRefreshToken = tokens.RefreshToken;
                App.Settings.Save();
                var pixiv = new PixivPlatform();
                pixiv.SetAccessToken(tokens.AccessToken);
                return pixiv;
            }

            case DeviantArt:
            {
                if (string.IsNullOrEmpty(settings.DeviantArtClientId) || string.IsNullOrEmpty(settings.DeviantArtClientSecret))
                    throw new InvalidOperationException("Add your DeviantArt API credentials in Settings first.");
                var deviant = new DeviantArtPlatform(settings.DeviantArtClientId, settings.DeviantArtClientSecret, settings.IncludeMatureContent);
                await deviant.AuthenticateAsync(cancellationToken);
                return deviant;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(platform));
        }
    }
}