using System.Text.Json;

namespace MuziekDownloader.Models;

internal sealed class AppSettings
{
    public string OutputFolder { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Muziek Downloader");
    public bool SkipExisting { get; set; } = true;
    public bool EmbedThumbnail { get; set; } = true;
    public bool AddMetadata { get; set; } = true;

    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MuziekDownloader");
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            return File.Exists(SettingsFile)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new()
                : new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsFolder);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
