using System.IO.Compression;
using ImageMagick;

namespace Artbrewify.Core;

public static class UgoiraConverter
{
    public static async Task SaveAsync(
        Stream zipStream,
        IReadOnlyList<UgoiraFrame> frames,
        string destinationPath,
        bool asGif,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        if (!asGif)
        {
            var framesDir = Path.ChangeExtension(destinationPath, null) + "_frames";
            Directory.CreateDirectory(framesDir);
            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = archive.GetEntry(frame.FileName);
                entry?.ExtractToFile(Path.Combine(framesDir, FileName.Sanitize(frame.FileName)), overwrite: true);
            }
            return;
        }

        using var collection = new MagickImageCollection();
        foreach (var frame in frames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = archive.GetEntry(frame.FileName);
            if (entry is null) continue;
            await using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            await entryStream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            var image = new MagickImage(buffer)
            {
                AnimationDelay = (uint)Math.Max(1, frame.DelayMs / 10),
                GifDisposeMethod = GifDisposeMethod.Background
            };
            collection.Add(image);
        }

        if (collection.Count == 0) return;
        collection.Quantize();
        await collection.WriteAsync(destinationPath, MagickFormat.Gif, cancellationToken);
    }
}