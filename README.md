# Muziek Downloader

![Muziek Downloader-pictogram](Assets/app-icon.png)

Een lokale Windows-app om audio van toegestane online video's als MP3 op te slaan. Geen account, activatie of apparatenlimiet.

## Ontwikkelen

Vereist de .NET 8 SDK.

```powershell
dotnet build
dotnet run
```

Bij de eerste update/download haalt de app `yt-dlp` en FFmpeg op in `%LOCALAPPDATA%\MuziekDownloader\tools`. Instellingen staan los van de programmabestanden en blijven bij een update behouden.

Gebruik de app uitsluitend voor materiaal dat je mag downloaden. De app omzeilt geen DRM of betaalmuren.

Copyright Â© 2026 App4you2 internetservice B.V.
