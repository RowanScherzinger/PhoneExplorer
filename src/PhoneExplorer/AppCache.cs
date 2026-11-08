using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PhoneExplorer;

/// <summary>
/// The app's one disk cache: opened files, phone files pulled for duplicate checks, thumbnails and the
/// PC folder index. Strictly managed:
/// - It lives in its own "PhoneExplorer cache" folder, marked with a marker file. Nothing outside that folder is ever touched.
/// - Every file it keeps is listed in index.json. At startup, anything in the folder that isn't listed
///   (left by a crash, or by an older version) is deleted, as are listed files that have gone missing.
/// - It never grows past its limit: the least recently used entries are evicted first.
/// - Writes are staged in tmp\ and moved into place when complete, so a half-written file is never served.
/// Thread-safe: the UI, the phone worker and the duplicate scan all use it.
/// </summary>
public sealed class AppCache : IDisposable
{
    public const string FolderName = "PhoneExplorer cache";
    const string MarkerName = "phoneexplorer-cache.marker";
    const string IndexName = "index.json";
    const string BlobDir = "b", StageDir = "tmp";

    sealed class Entry
    {
        public string Path { get; set; } = "";
        public long Size { get; set; }
        public long Used { get; set; }
    }

    readonly object gate = new();
    readonly System.Threading.Timer saveTimer;
    Dictionary<string, Entry> entries = new();
    long used;
    bool dirty;

    /// <summary>The folder the cache lives in, inside the chosen location.</summary>
    public string Root { get; private set; } = "";
    public long Limit { get; private set; }
    public long Used { get { lock (gate) return used; } }

    /// <summary>The default location: %LOCALAPPDATA%\PhoneExplorer.</summary>
    public static string DefaultLocation =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhoneExplorer");

    public static string RootFor(string location) =>
        Path.Combine(string.IsNullOrWhiteSpace(location) ? DefaultLocation : location.Trim(), FolderName);

    public AppCache(string location, long limit)
    {
        saveTimer = new System.Threading.Timer(_ => Save(), null, Timeout.Infinite, Timeout.Infinite);
        Limit = limit;
        Open(location);
        RemoveLegacyTemp();
    }

    /// <summary>Whether a path is inside the cache, so the duplicate scan never mistakes a cached copy for a backup.</summary>
    public bool Contains(string path)
    {
        var root = Path.GetFullPath(Root).TrimEnd('\\') + "\\";
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    void Open(string location)
    {
        lock (gate)
        {
            Root = RootFor(location);
            entries = new();
            used = 0;
            try
            {
                bool fresh = !Directory.Exists(Root) || !Directory.EnumerateFileSystemEntries(Root).Any();
                Directory.CreateDirectory(Root);
                var marker = Path.Combine(Root, MarkerName);
                if (fresh && !File.Exists(marker)) File.WriteAllText(marker, "Phone Explorer keeps its cache here. Everything in this folder can be deleted.");
                if (!File.Exists(marker)) return; // A folder we didn't create: use nothing in it, delete nothing in it.

                LoadIndex();
                Sweep();
                Evict(null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { }
            Save();
        }
    }

    bool Usable => File.Exists(Path.Combine(Root, MarkerName));

    void LoadIndex()
    {
        Dictionary<string, Entry>? loaded = null;
        try { loaded = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(Path.Combine(Root, IndexName))); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        foreach (var (key, e) in loaded ?? new())
        {
            // Only relative paths inside the blob folder; a listed file must still be there at its recorded size.
            if (e.Path.Contains("..") || Path.IsPathRooted(e.Path) || !e.Path.StartsWith(BlobDir + "\\")) continue;
            var full = Path.Combine(Root, e.Path);
            if (!File.Exists(full) || new FileInfo(full).Length != e.Size) continue;
            entries[key] = e;
            used += e.Size;
        }
        dirty = true;
    }

    /// <summary>Deletes everything in the cache folder that the index doesn't list, then empty folders.</summary>
    void Sweep()
    {
        var keep = entries.Values.Select(e => Path.Combine(Root, e.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        keep.Add(Path.Combine(Root, MarkerName));
        keep.Add(Path.Combine(Root, IndexName));
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).ToList())
            if (!keep.Contains(file)) TryDeleteFile(file);
        foreach (var dir in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).ToList())
            if (!Directory.EnumerateFileSystemEntries(dir).Any()) try { Directory.Delete(dir); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    static void TryDeleteFile(string file)
    {
        try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Removes the unmanaged temp folder that versions before 0.3 copied opened files into.</summary>
    void RemoveLegacyTemp()
    {
        var legacy = Path.Combine(Path.GetTempPath(), "PhoneExplorer");
        // Never when the cache itself was put there.
        if (Path.GetFullPath(Root).StartsWith(Path.GetFullPath(legacy).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return;
        try { Directory.Delete(legacy, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    static string Hash(string key) => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)))[..20].ToLowerInvariant();

    /// <summary>The cached file for a key, or null. Marks it as recently used.</summary>
    public string? Get(string key)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var e)) return null;
            var full = Path.Combine(Root, e.Path);
            if (!File.Exists(full)) { Drop(key); return null; }
            e.Used = DateTime.UtcNow.Ticks;
            MarkDirty();
            return full;
        }
    }

    /// <summary>A new empty folder to write a file into; pass the finished file to Commit. Null when the cache can't be used.</summary>
    public string? Stage()
    {
        lock (gate)
        {
            if (!Usable) return null;
            var dir = Path.Combine(Root, StageDir, Guid.NewGuid().ToString("N"));
            try { Directory.CreateDirectory(dir); return dir; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
        }
    }

    /// <summary>Moves a staged file into the cache under a key (keeping its file name) and returns its new path.
    /// Returns null, and removes the staged file, if the cache moved or the file can't be kept.</summary>
    public string? Commit(string key, string stagedFile)
    {
        lock (gate)
        {
            var stageDir = Path.GetDirectoryName(stagedFile)!;
            try
            {
                if (!Usable || !stagedFile.StartsWith(Path.Combine(Root, StageDir) + "\\", StringComparison.OrdinalIgnoreCase) || !File.Exists(stagedFile))
                    return null;
                Drop(key);
                var rel = Path.Combine(BlobDir, Hash(key), Path.GetFileName(stagedFile));
                var full = Path.Combine(Root, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                if (File.Exists(full)) File.Delete(full);
                File.Move(stagedFile, full);
                long size = new FileInfo(full).Length;
                entries[key] = new Entry { Path = rel, Size = size, Used = DateTime.UtcNow.Ticks };
                used += size;
                Evict(key);
                MarkDirty();
                return entries.ContainsKey(key) ? full : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
            finally
            {
                try { if (Directory.Exists(stageDir)) Directory.Delete(stageDir, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>Removes a staged folder that wasn't committed (a failed or cancelled write). Safe to call after Commit.</summary>
    public void Discard(string stageDir)
    {
        lock (gate)
        {
            if (!stageDir.StartsWith(Path.Combine(Root, StageDir) + "\\", StringComparison.OrdinalIgnoreCase)) return;
            try { if (Directory.Exists(stageDir)) Directory.Delete(stageDir, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Stores a file the caller writes: write(path) must create the file at path.</summary>
    public string? Put(string key, string fileName, Action<string> write)
    {
        var dir = Stage();
        if (dir == null) return null;
        try
        {
            var path = Path.Combine(dir, fileName);
            write(path);
            return Commit(key, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException) { return null; }
        finally { Discard(dir); }
    }

    /// <summary>Removes every entry whose key starts with prefix (for example all sizes of one file's thumbnail).</summary>
    public void RemovePrefix(string prefix)
    {
        lock (gate)
        {
            foreach (var key in entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList()) Drop(key);
            MarkDirty();
        }
    }

    public void Remove(string key)
    {
        lock (gate) { Drop(key); MarkDirty(); }
    }

    /// <summary>Deletes an entry's file and folder. A file another app has open stays listed and is retried later.</summary>
    bool Drop(string key)
    {
        if (!entries.TryGetValue(key, out var e)) return true;
        var full = Path.Combine(Root, e.Path);
        try
        {
            if (File.Exists(full)) File.Delete(full);
            var dir = Path.GetDirectoryName(full)!;
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(full)) return false;
        }
        entries.Remove(key);
        used -= e.Size;
        return true;
    }

    /// <summary>Evicts least recently used entries until the cache fits its limit. keep is never evicted.</summary>
    void Evict(string? keep)
    {
        if (used <= Limit) return;
        foreach (var (key, _) in entries.Where(kv => kv.Key != keep).OrderBy(kv => kv.Value.Used).ToList())
        {
            if (used <= Limit) break;
            Drop(key);
        }
        // Still too big: the newest entry alone is over the limit, so it can't be kept either.
        if (used > Limit && keep != null) Drop(keep);
    }

    public void SetLimit(long limit)
    {
        lock (gate) { Limit = limit; Evict(null); MarkDirty(); }
    }

    /// <summary>Deletes every cached file. Files open in another app are kept until they're closed.</summary>
    public void Clear()
    {
        lock (gate)
        {
            foreach (var key in entries.Keys.ToList()) Drop(key);
            if (Usable) try { Sweep(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            MarkDirty();
        }
        Save();
    }

    /// <summary>
    /// Whether the cache can live at a location: its "PhoneExplorer cache" folder is new, empty, or
    /// already a Phone Explorer cache. A folder with someone else's files in it is never used.
    /// </summary>
    public static bool CanUse(string location)
    {
        try
        {
            var root = RootFor(location);
            return !Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Any() || File.Exists(Path.Combine(root, MarkerName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) { return false; }
    }

    public bool IsAt(string location) =>
        string.Equals(Path.GetFullPath(RootFor(location)), Path.GetFullPath(Root), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Moves the cache to a new location, most recently used files first. On the same drive each move is
    /// a rename. Joins any Phone Explorer cache already there. When cancelled, or when the new drive is
    /// full, the files not yet moved are deleted: it's a cache, and nothing is left behind in the old
    /// folder either way. Blocks the cache's other users while it runs. Returns how many files moved.
    /// </summary>
    public int Relocate(string location, Action<int, int>? progress = null, Func<bool>? cancelled = null)
    {
        lock (gate)
        {
            if (IsAt(location)) return 0;
            var oldRoot = Root;
            bool oldUsable = Usable;
            var old = oldUsable ? entries.OrderByDescending(kv => kv.Value.Used).ToList() : new();
            Save();
            Open(location);
            int moved = 0;
            bool stop = !Usable;
            for (int i = 0; i < old.Count; i++)
            {
                var (key, e) = old[i];
                var from = Path.Combine(oldRoot, e.Path);
                if (!stop && cancelled?.Invoke() == true) stop = true;
                if (!stop && !entries.ContainsKey(key) && File.Exists(from))
                {
                    progress?.Invoke(i, old.Count);
                    var to = Path.Combine(Root, e.Path);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                        if (File.Exists(to)) File.Delete(to);
                        File.Move(from, to);
                        entries[key] = e;
                        used += e.Size;
                        moved++;
                        // Keep the new index current, so a crash part-way loses nothing that was moved.
                        if (moved % 200 == 0) { dirty = true; Save(); }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        TryDeleteFile(to);
                        // ERROR_DISK_FULL / ERROR_HANDLE_DISK_FULL: nothing more will fit.
                        if ((ex.HResult & 0xFFFF) is 0x70 or 0x27) stop = true;
                    }
                }
                TryDeleteFile(from);
            }
            if (oldUsable)
                try { Directory.Delete(oldRoot, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            Evict(null);
            dirty = true;
            Save();
            return moved;
        }
    }

    void MarkDirty()
    {
        dirty = true;
        // After Dispose (the app is closing), late writes stay unlisted and are swept at the next start.
        try { saveTimer.Change(5000, Timeout.Infinite); } catch (ObjectDisposedException) { }
    }

    /// <summary>Writes the index (staged, then swapped in, so a crash leaves the old one).</summary>
    public void Save()
    {
        lock (gate)
        {
            if (!dirty || !Usable) return;
            try
            {
                var path = Path.Combine(Root, IndexName);
                var temp = path + ".new";
                File.WriteAllText(temp, JsonSerializer.Serialize(entries));
                File.Move(temp, path, overwrite: true);
                dirty = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public void Dispose()
    {
        saveTimer.Dispose();
        Save();
    }
}
