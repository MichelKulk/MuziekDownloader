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
    public string FfprobePath => Path.Combine(ToolFolder, "ffprobe.exe");
    public bool ToolsReady => File.Exists(YtDlpPath) && File.Exists(FfmpegPath) && File.Exists(FfprobePath);

    public ToolManager() => _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MuziekDownloader", "0.1"));

    public async Task UpdateYtDlpAsync(IProgress<string>? status = null)
    {
        Directory.CreateDirectory(ToolFolder);
        status?.Report("Downloadcomponent ophalen…");
        var temp = YtDlpPath + ".new";
        await DownloadAsync("https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp.exe", temp, status, "Downloadcomponent");
        File.Move(temp, YtDlpPath, true);
        status?.Report("Downloadcomponent is bijgewerkt");
    }

    public async Task EnsureFfmpegAsync(IProgress<string>? status = null)
    {
        Directory.CreateDirectory(ToolFolder);
        var zipPath = Path.Combine(ToolFolder, "ffmpeg.zip");
        if (File.Exists(FfmpegPath) && File.Exists(FfprobePath))
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            return;
        }
        status?.Report("MP3-omzetter ophalen (eenmalig)…");
        await DownloadAsync("https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", zipPath, status, "MP3-omzetter");
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

    private async Task DownloadAsync(string url, string target, IProgress<string>? status, string label)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(target);
        var buffer = new byte[128 * 1024];
        long received = 0;
        int read;
        int lastPercentage = -1;
        while ((read = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read));
            received += read;
            if (total is > 0)
            {
                var percentage = (int)(received * 100 / total.Value);
                if (percentage != lastPercentage)
                {
                    lastPercentage = percentage;
                    status?.Report($"{label} downloaden: {percentage}%");
                }
            }
            else
            {
                status?.Report($"{label} downloaden: {received / 1024 / 1024} MB");
            }
        }
    }
}

