using System.IO.Compression;
using System.Net.Http.Headers;

namespace MuziekDownloader.Services;

internal sealed class ToolManager
{
    private readonly HttpClient _http = new();
    public string ToolFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MuziekDownloader", "tools");
    public string YtDlpPath => Path.Combine(ToolFolder, "yt-dlp.exe");
    public string FfmpegPath => Path.Combine(ToolFolder, "ffmpeg.exe");
    public bool ToolsReady => File.Exists(YtDlpPath) && File.Exists(FfmpegPath);

    public ToolManager() => _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MuziekDownloader", "0.1"));

    public async Task UpdateYtDlpAsync(IProgress<string>? status = null)
    {
        Directory.CreateDirectory(ToolFolder);
        status?.Report("Downloadcomponent ophalenâ€¦");
        var temp = YtDlpPath + ".new";
        await DownloadAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", temp);
        File.Move(temp, YtDlpPath, true);
        status?.Report("Downloadcomponent is bijgewerkt");
    }

    public async Task EnsureFfmpegAsync(IProgress<string>? status = null)
    {
        if (File.Exists(FfmpegPath)) return;
        Directory.CreateDirectory(ToolFolder);
        status?.Report("MP3-omzetter ophalen (eenmalig)â€¦");
        var zipPath = Path.Combine(ToolFolder, "ffmpeg.zip");
        await DownloadAsync("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", zipPath);
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe" })
            {
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("/bin/" + name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"{name} ontbreekt in het pakket.");
                entry.ExtractToFile(Path.Combine(ToolFolder, name), true);
            }
        }
        File.Delete(zipPath);
        status?.Report("MP3-omzetter is gereed");
    }

    private async Task DownloadAsync(string url, string target)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(target);
        await input.CopyToAsync(output);
    }
}
