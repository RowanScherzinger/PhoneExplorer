using System.Text.Json;
using System.Text.RegularExpressions;

namespace PhoneExplorer;

/// <summary>How sure a phone file's match on the PC is, best first.</summary>
public enum DupTier
{
    /// <summary>Exact mode: the same bytes, the same pixels or (video) the same size, length, resolution and format.</summary>
    Exact,
    /// <summary>Same size and format, and the same name or the same time taken.</summary>
    SameFile,
    /// <summary>Same name and time taken but a different file, for example converted from HEIC to JPG.</summary>
    SamePhoto,
}

/// <summary>A phone file's best match on the PC.</summary>
public sealed record FileMatch(DupTier Tier, string PcPath, long PcSize, string Reason);

/// <summary>How a whole item (with its edits, Live Photo clip and edit data) stands against the PC.</summary>
public sealed record EntryMatch(DupTier? Tier, FileMatch Primary, List<string> Missing)
{
    /// <summary>Every file that deleting would remove (except .AAE edit data) has a match.</summary>
    public bool Complete => Missing.Count == 0;
}

/// <summary>A file on this PC as the duplicate check sees it. Properties are read only for files that could match.</summary>
public sealed class PcFile
{
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public long Written { get; set; }
    public bool HasProps { get; set; }
    public DateTime? Taken { get; set; }
    public DateTime? Encoded { get; set; }
    public DateTime? Modified { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long DurationTicks { get; set; }

    public string Name => System.IO.Path.GetFileName(Path);
}

/// <summary>Results of a duplicate check, keyed by phone folder, name and size so they survive a rescan.</summary>
public sealed class DupResult
{
    public readonly Dictionary<string, FileMatch> Files = new();
    public readonly List<string> Folders;
    public readonly bool Exact;
    /// <summary>Exact mode: matches that couldn't be checked exactly (not copied from the phone, or no decoder), kept as Same file.</summary>
    public int Uncompared;

    public DupResult(List<string> folders, bool exact) { Folders = folders; Exact = exact; }

    public static string Key(MediaFile f) => $"{f.Folder}|{f.Name}|{f.Size}";

    public FileMatch? For(MediaFile f) => Files.GetValueOrDefault(Key(f));

    /// <summary>
    /// Null when the original has no match. Otherwise the weakest tier over every file that a delete
    /// removes (edit data excepted), and the ones with no match listed in Missing.
    /// </summary>
    public EntryMatch? For(MediaEntry e)
    {
        var primary = For(e.Primary);
        if (primary == null) return null;
        var tier = primary.Tier;
        var missing = new List<string>();
        foreach (var (file, role) in e.Extras)
        {
            if (role == FileRole.Sidecar) continue;
            var m = For(file);
            if (m == null) missing.Add(role switch
            {
                FileRole.Edited => "the edited version",
                FileRole.Live => "the Live Photo clip",
                _ => "the edited Live Photo clip",
            });
            else if (m.Tier > tier) tier = m.Tier;
        }
        return new EntryMatch(missing.Count == 0 ? tier : null, primary, missing);
    }

    /// <summary>Drops matches whose PC copy has gone or changed since the check. Returns how many were dropped.</summary>
    public int Revalidate(IEnumerable<MediaFile> files)
    {
        int dropped = 0;
        foreach (var f in files)
        {
            var key = Key(f);
            if (!Files.TryGetValue(key, out var m)) continue;
            bool there;
            try { there = File.Exists(m.PcPath) && new FileInfo(m.PcPath).Length == m.PcSize; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { there = false; }
            if (!there) { Files.Remove(key); dropped++; }
        }
        return dropped;
    }
}

/// <summary>
/// Finds phone files that are already in folders on this PC. Runs off the UI thread; phone files are
/// pulled only through the PhoneWorker (the pull delegate), and only for files that already matched
/// on name, size or time.
/// </summary>
public sealed class DuplicateFinder
{
    const string IndexKey = "pcindex";
    static readonly TimeSpan TimeSlack = TimeSpan.FromSeconds(2);
    static readonly Regex CopySuffix = new(@"^(.*?)(?: \(\d+\))+$");

    readonly AppCache cache;
    readonly Func<MediaFile, Task<(string? Path, string? Error)>> pull;
    readonly Func<bool> cancelled;
    readonly Action<string, int, int> progress;

    public DuplicateFinder(AppCache cache, Func<MediaFile, Task<(string?, string?)>> pull, Func<bool> cancelled, Action<string, int, int> progress)
    {
        this.cache = cache;
        this.pull = pull;
        this.cancelled = cancelled;
        this.progress = progress;
    }

    /// <summary>"IMG_1234 (2).JPG" -> "IMG_1234"; case-insensitive.</summary>
    static string Stem(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var m = CopySuffix.Match(stem);
        return (m.Success ? m.Groups[1].Value : stem).ToUpperInvariant();
    }

    static string Ext(string name) => Path.GetExtension(name).ToUpperInvariant();

    static bool Near(DateTime a, DateTime? b) => b != null && (a - b.Value).Duration() <= TimeSlack;

    /// <summary>The phone's date (taken, or created) against every date the PC copy may have kept.</summary>
    static bool SameTime(MediaFile f, PcFile p) =>
        f.Date != DateTime.MinValue && (Near(f.Date, p.Taken) || Near(f.Date, p.Encoded) || Near(f.Date, p.Modified));

    public async Task<DupResult> Run(List<MediaFile> phone, List<string> folders, bool exact)
    {
        var result = new DupResult(folders, exact);
        var pc = await Task.Run(() => ListPc(folders));
        if (cancelled()) return result;

        var bySize = pc.GroupBy(p => p.Size).ToDictionary(g => g.Key, g => g.ToList());
        var byStem = pc.GroupBy(p => Stem(p.Name)).ToDictionary(g => g.Key, g => g.ToList());

        // Edit data (.AAE) and other non-media files never count: they're not photos and a delete keeps no promise about them.
        var wanted = phone.Where(f => f.Kind != MediaKind.Other && f.Size > 0).ToList();
        for (int i = 0; i < wanted.Count; i++)
        {
            if (cancelled()) break;
            var f = wanted[i];
            progress($"Comparing {f.Name}", i, wanted.Count);
            var candidates = bySize.GetValueOrDefault(f.Size, new()).Concat(byStem.GetValueOrDefault(Stem(f.Name), new())).Distinct().ToList();
            if (candidates.Count == 0) continue;
            var match = await Task.Run(() => MatchOne(f, candidates, exact, result));
            if (match != null) result.Files[DupResult.Key(f)] = match;
        }
        await Task.Run(() => SaveIndex(pc));
        return result;
    }

    // ---- PC side ------------------------------------------------------------------------

    /// <summary>Lists media files in the folders (no property reads yet), reusing what the cached index knows.</summary>
    List<PcFile> ListPc(List<string> folders)
    {
        var known = LoadIndex();
        var list = new List<PcFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true, IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
        };
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                foreach (var path in Directory.EnumerateFiles(folder, "*", options))
                {
                    if (cancelled()) return list;
                    if (Library.KindOf(Ext(path).TrimStart('.')) == MediaKind.Other) continue;
                    // A cached copy of a phone file is not a backup of it.
                    if (cache.Contains(path) || !seen.Add(path)) continue;
                    FileInfo info;
                    try { info = new FileInfo(path); if (!info.Exists) continue; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { continue; }
                    long written = info.LastWriteTimeUtc.Ticks;
                    var p = known.GetValueOrDefault(path);
                    if (p == null || p.Size != info.Length || p.Written != written)
                        p = new PcFile { Path = path, Size = info.Length, Written = written };
                    list.Add(p);
                    if (list.Count % 500 == 0) progress($"Reading PC folders: {list.Count:N0} files", 0, 0);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return list;
    }

    static void ReadProps(PcFile p)
    {
        if (p.HasProps) return;
        try
        {
            var (taken, encoded, modified, w, h, duration) = Shell.LocalProperties(p.Path);
            (p.Taken, p.Encoded, p.Modified, p.Width, p.Height, p.DurationTicks) = (taken, encoded, modified, w, h, duration?.Ticks ?? 0);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or InvalidCastException) { }
        p.HasProps = true;
    }

    Dictionary<string, PcFile> LoadIndex()
    {
        try
        {
            if (cache.Get(IndexKey) is { } path)
                return (JsonSerializer.Deserialize<List<PcFile>>(File.ReadAllText(path)) ?? new())
                    .GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        return new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Keeps only this run's files, so the index never outgrows the folders being checked.</summary>
    void SaveIndex(List<PcFile> pc) =>
        cache.Put(IndexKey, "pcindex.json", path => File.WriteAllText(path, JsonSerializer.Serialize(pc)));

    // ---- matching -----------------------------------------------------------------------

    FileMatch? MatchOne(MediaFile f, List<PcFile> candidates, bool exact, DupResult result)
    {
        foreach (var c in candidates) ReadProps(c);
        string stem = Stem(f.Name), ext = Ext(f.Name);

        // Same file: same size and format, and the same name (allowing "name (2)") or the same time.
        var same = candidates.FirstOrDefault(c => c.Size == f.Size && Ext(c.Name) == ext && Stem(c.Name) == stem)
                   ?? candidates.FirstOrDefault(c => c.Size == f.Size && Ext(c.Name) == ext && SameTime(f, c));
        if (same != null)
        {
            var reason = Stem(same.Name) == stem ? "same name and size" : "same size and time, renamed";
            if (!exact) return new FileMatch(DupTier.SameFile, same.Path, same.Size, reason);
            var verdict = Verify(f, same, result);
            if (verdict.Exact) return new FileMatch(DupTier.Exact, same.Path, same.Size, verdict.Reason);
            // Proven different: not a copy, whatever the name and size say.
            if (verdict.Checked) return null;
            // Couldn't check: no worse than without exact mode, and counted in the summary.
            result.Uncompared++;
            return new FileMatch(DupTier.SameFile, same.Path, same.Size, $"{reason}; {verdict.Reason}");
        }

        // Same photo, different file: the same name stem and time, a different size or format (converted or re-saved).
        var kind = f.Kind == MediaKind.Video ? MediaKind.Video : MediaKind.Photo;
        var similar = candidates.Where(c => Stem(c.Name) == stem && SameTime(f, c) &&
                                            (Library.KindOf(Ext(c.Name).TrimStart('.')) == MediaKind.Video ? MediaKind.Video : MediaKind.Photo) == kind).ToList();
        if (similar.Count == 0) return null;

        if (kind == MediaKind.Video && !exact)
            // Length and resolution would need the whole video copied from the phone, so they're only checked in exact mode.
            return new FileMatch(DupTier.SamePhoto, similar[0].Path, similar[0].Size, "same name and time, different file; length not checked");

        // Read the phone file's resolution (and length) from a local copy.
        var local = Pull(f);
        if (local == null) return null;
        var mine = new PcFile { Path = local };
        ReadProps(mine);
        foreach (var c in similar)
        {
            bool sameSize = mine.Width > 0 && ((c.Width == mine.Width && c.Height == mine.Height) || (c.Width == mine.Height && c.Height == mine.Width));
            if (!sameSize) continue;
            if (kind == MediaKind.Video && Math.Abs(c.DurationTicks - mine.DurationTicks) > TimeSpan.TicksPerSecond) continue;
            if (exact && kind != MediaKind.Video && Wic.SamePixels(local, c.Path) == true)
                return new FileMatch(DupTier.Exact, c.Path, c.Size, "same pixels, different file");
            return new FileMatch(DupTier.SamePhoto, c.Path, c.Size,
                kind == MediaKind.Video ? "same name, time, length and resolution; different file" : "same name, time and resolution; different file");
        }
        return null;
    }

    /// <summary>Exact mode for a same-size match: identical bytes, or failing that identical pixels (images) or metadata (videos).
    /// Checked is false when the comparison couldn't be made at all.</summary>
    (bool Exact, bool Checked, string Reason) Verify(MediaFile f, PcFile c, DupResult result)
    {
        var local = Pull(f);
        if (local == null) return (false, false, "couldn't copy it from the phone to compare");
        if (SameBytes(local, c.Path)) return (true, true, "identical file");
        if (f.Kind == MediaKind.Video)
        {
            var mine = new PcFile { Path = local };
            ReadProps(mine);
            bool same = mine.Width == c.Width && mine.Height == c.Height && mine.DurationTicks == c.DurationTicks && mine.DurationTicks > 0;
            return (same, true, same ? "same size, length, resolution and format" : "contents differ");
        }
        return Wic.SamePixels(local, c.Path) switch
        {
            true => (true, true, "same pixels"),
            false => (false, true, "pictures differ"),
            _ => (false, false, "couldn't compare pictures (Windows has no decoder for this format)"),
        };
    }

    string? Pull(MediaFile f) => pull(f).GetAwaiter().GetResult().Path;

    static bool SameBytes(string a, string b)
    {
        try
        {
            using var x = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
            using var y = new FileStream(b, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
            if (x.Length != y.Length) return false;
            var bx = new byte[1 << 20];
            var by = new byte[1 << 20];
            while (true)
            {
                int n = x.ReadAtLeast(bx, bx.Length, throwOnEndOfStream: false);
                int m = y.ReadAtLeast(by, by.Length, throwOnEndOfStream: false);
                if (n != m || !bx.AsSpan(0, n).SequenceEqual(by.AsSpan(0, m))) return false;
                if (n == 0) return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
