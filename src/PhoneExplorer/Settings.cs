using System.Text.Json;

namespace PhoneExplorer;

public sealed class Settings
{
    public string Destination { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Phone");
    public bool Organise { get; set; } = true;
    public bool IncludeEdits { get; set; } = true;
    public bool IncludeLive { get; set; } = false;
    public bool IncludeSidecars { get; set; } = false;
    public int ThumbSize { get; set; } = 150;
    public bool ShowPhotos { get; set; } = true;
    public bool ShowVideos { get; set; } = true;
    public bool ShowImages { get; set; } = true;
    public bool ShowOther { get; set; } = false;

    static string? dataDir;

    /// <summary>%APPDATA%\PhoneExplorer. The first time, moves over the folder the app used when it was iPhoneExplorer.</summary>
    public static string DataDir
    {
        get
        {
            if (dataDir != null) return dataDir;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            dataDir = Path.Combine(appData, "PhoneExplorer");
            var old = Path.Combine(appData, "iPhoneExplorer");
            if (!Directory.Exists(dataDir) && Directory.Exists(old))
            {
                try { Directory.Move(old, dataDir); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { dataDir = old; }
            }
            return dataDir;
        }
    }

    static string FilePath => Path.Combine(DataDir, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
