using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MuziekDownloader.Models;

namespace MuziekDownloader.Services;

internal sealed partial class YtDlpService(ToolManager tools)
{
    public async Task<DownloadItem> InspectAsync(string url, bool allowPlaylist, CancellationToken token = default)
    {
        var args = allowPlaylist
            ? $"--flat-playlist --playlist-items 1 --dump-single-json -- {Q(url)}"
            : $"--no-playlist --dump-single-json -- {Q(url)}";
        var (exit, output, error) = await RunAsync(args, null, token);
        if (exit != 0) throw new InvalidOperationException(CleanError(error));
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;
        var isPlaylist = root.TryGetProperty("_type", out var type) && type.GetString() == "playlist";
        var duration = root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
            ? TimeSpan.FromSeconds(d.GetDouble()).ToString(@"h\:mm\:ss") : "";
        return new DownloadItem {
            Url = url,
            Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Onbekende titel" : "Onbekende titel",
            Duration = duration,
            IsPlaylist = isPlaylist,
            Status = isPlaylist ? "Afspeellijst gereed" : "Gereed"
        };
    }

    public async Task DownloadAsync(DownloadItem item, string folder, bool playlist, bool skipExisting,
        bool embedThumbnail, bool addMetadata, string outputFormat, int videoHeight,
        IProgress<(int percent, string status)> progress, CancellationToken token)
    {
        Directory.CreateDirectory(folder);
        var output = playlist ? Path.Combine(folder, "%(playlist_title)s", "%(title)s.%(ext)s") : Path.Combine(folder, "%(title)s.%(ext)s");
        var isMp4 = outputFormat.Equals("MP4", StringComparison.OrdinalIgnoreCase);
        var mediaArguments = isMp4
            ? BuildMp4Arguments(videoHeight)
            : "-f bestaudio/best -x --audio-format mp3 --audio-quality 0";
        var args = $"{(playlist ? "--yes-playlist" : "--no-playlist")} {mediaArguments} " +
                   $"--ffmpeg-location {Q(tools.ToolFolder)} --newline --windows-filenames -o {Q(output)} " +
                   $"{(skipExisting ? "--no-overwrites" : "--force-overwrites")} " +
                   $"{(embedThumbnail ? "--embed-thumbnail" : "")} {(addMetadata ? "--embed-metadata" : "")} -- {Q(item.Url)}";
        var (exit, _, error) = await RunAsync(args, line => {
            var match = ProgressRegex().Match(line);
            if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                progress.Report(((int)value, "Downloaden"));
            else if (line.Contains("ExtractAudio", StringComparison.OrdinalIgnoreCase)) progress.Report((96, "Omzetten naar MP3"));
            else if (line.Contains("Merger", StringComparison.OrdinalIgnoreCase) || line.Contains("VideoRemuxer", StringComparison.OrdinalIgnoreCase))
                progress.Report((97, "Beeld en geluid samenvoegen"));
        }, token);
        if (exit != 0) throw new InvalidOperationException(CleanError(error));
        progress.Report((100, "Voltooid"));
    }

    private static string BuildMp4Arguments(int videoHeight)
    {
        var heightFilter = videoHeight > 0 ? $"[height<={videoHeight}]" : "";
        return $"-f \"bestvideo{heightFilter}[ext=mp4]+bestaudio[ext=m4a]/best{heightFilter}[ext=mp4]/best{heightFilter}\" --merge-output-format mp4 --remux-video mp4";
    }

    private async Task<(int exit, string output, string error)> RunAsync(string arguments, Action<string>? outputLine, CancellationToken token)
    {
        var psi = new ProcessStartInfo(tools.YtDlpPath, arguments) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        using var process = new Process { StartInfo = psi };
        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); outputLine?.Invoke(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); outputLine?.Invoke(e.Data); } };
        process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
        await process.WaitForExitAsync(token);
        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string Q(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
    private static string CleanError(string error) => string.IsNullOrWhiteSpace(error) ? "De opdracht is mislukt." : error.Trim().Split('\n').Last().Trim();
    [GeneratedRegex(@"\[download\]\s+([0-9.]+)%")]
    private static partial Regex ProgressRegex();
}

