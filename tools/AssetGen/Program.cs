using ImageMagick;

var logo = args.Length > 0 ? args[0] : "assets/branding/artbrewify-logo.png";
var outDir = args.Length > 1 ? args[1] : "src/Artbrewify/Assets";
Directory.CreateDirectory(outDir);

using (var logoImage = new MagickImage(logo))
{
    logoImage.Resize(512, 512);
    logoImage.Write(Path.Combine(outDir, "logo.png"), MagickFormat.Png);
}

using var icons = new MagickImageCollection();
foreach (var size in new uint[] { 256, 48, 32, 16 })
{
    using var frame = new MagickImage(logo);
    frame.Resize(size, size);
    frame.Format = MagickFormat.Ico;
    icons.Add(frame.Clone());
}
icons.Write(Path.Combine(outDir, "app.ico"), MagickFormat.Ico);

Console.WriteLine($"Wrote logo.png and app.ico to {outDir}");