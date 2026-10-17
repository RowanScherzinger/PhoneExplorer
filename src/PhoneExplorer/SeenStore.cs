namespace PhoneExplorer;

/// <summary>
/// Remembers which files have been seen on each device, so files that appear later (new photos,
/// or photos unhidden on the phone) can be listed as new. The phone reports no "date added", so
/// this is the only reliable signal. Stored as one "folder/name|size" line per file.
/// </summary>
public sealed class SeenStore
{
    readonly string path;

    /// <summary>What was known when this session first saw the device; null if never seen before.</summary>
    public HashSet<string>? Baseline { get; private set; }

    public SeenStore(string deviceName)
    {
        var safe = string.Concat(deviceName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        path = Path.Combine(Settings.DataDir, $"seen-{safe}.txt");
        try { Baseline = File.Exists(path) ? File.ReadAllLines(path).ToHashSet() : null; }
        catch (IOException) { Baseline = null; }
    }

    public static string Key(MediaFile f) => $"{f.Folder}/{f.Name}|{f.Size}";

    /// <summary>Records everything currently on the phone. The baseline is kept, so "new" lasts the whole session
    /// until the user marks everything as seen (resetBaseline).</summary>
    public void Remember(IEnumerable<MediaFile> files, bool resetBaseline = false)
    {
        var known = files.Select(Key).ToHashSet();
        if (Baseline != null && !resetBaseline) known.UnionWith(Baseline);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, known);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        // First time we see this device everything is "known", so later arrivals in this session still count as new.
        if (resetBaseline || Baseline == null) Baseline = known;
    }

    public bool IsNew(MediaFile f) => Baseline != null && !Baseline.Contains(Key(f));
}
