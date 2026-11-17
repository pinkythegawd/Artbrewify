namespace Artbrewify.Core.Platforms;

public interface IArtPlatform
{
    string Name { get; }
    bool RequiresLogin { get; }
    bool IsReady { get; }
    Task<ArtistGallery> FetchGalleryAsync(string input, IProgress<string>? status, CancellationToken cancellationToken);
}