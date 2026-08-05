namespace MuziekDownloader.Models;

internal sealed class DownloadItem
{
    public string Url { get; init; } = "";
    public string Title { get; set; } = "Link wordt gelezenâ€¦";
    public string Duration { get; set; } = "";
    public string Status { get; set; } = "Wachten";
    public int Progress { get; set; }
    public bool IsPlaylist { get; set; }
}
