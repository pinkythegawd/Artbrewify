# Artbrewify

**Brew your art collection.** A modern Windows desktop app that bulk-downloads an artist's full gallery from **ArtStation**, **Pixiv**, and **DeviantArt** — in original quality.

Artbrewify is a rebranded, rebuilt successor to [Artify](https://github.com/sentouki/Artify) by Rodion Chaykovskiy (MIT), modernized from .NET Core 3.1 / WPF to **.NET 8 / WinUI 3**.

## Features

- Download an artist's entire gallery from ArtStation, Pixiv, or DeviantArt
- Original full-resolution files, not previews
- Pixiv ugoira (animations) saved as animated GIF
- Modern Fluent dark UI with live download progress
- Concurrent downloads with retry, resume (already-downloaded files are skipped)
- Pixiv login via embedded browser (WebView2); tokens stored encrypted with Windows DPAPI
- Bring-your-own DeviantArt API app credentials — no shared secrets baked into the binary

## Requirements

- Windows 10 2004 (build 19041) or later
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build

## Build & run

```
dotnet build Artbrewify.sln
dotnet run --project src/Artbrewify
```

## Platform setup

| Platform | What you need |
|---|---|
| ArtStation | Nothing — public galleries work out of the box |
| Pixiv | A Pixiv account (log in inside the app) |
| DeviantArt | Your own API app credentials from [deviantart.com/developers](https://www.deviantart.com/developers) (client_credentials grant), entered in Settings |

## Responsible use

Download artwork for personal archiving only. Respect artists' rights and each platform's terms of service. Aggressive bulk downloading can get your IP or account rate-limited or banned — use sane concurrency settings.

## Credits

- Original [Artify](https://github.com/sentouki/Artify) — Rodion Chaykovskiy (MIT)
- Pixiv OAuth flow research — [azuline/pixiv-api](https://github.com/azuline/pixiv-api) and the pixivpy community
- Built with [WinUI 3 / Windows App SDK](https://learn.microsoft.com/windows/apps/winui/winui3/)

## License

[MIT](LICENSE) — preserves the original Artify copyright notice.