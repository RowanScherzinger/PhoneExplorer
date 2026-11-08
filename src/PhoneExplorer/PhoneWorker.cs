using System.Drawing;
using System.Runtime.InteropServices;

namespace PhoneExplorer;

/// <summary>One file to copy. Item is the name the user knows it by; Label adds which companion it is.</summary>
public sealed record CopyRequest(int Id, string Name, string RelativeDir, long Size, string Item, string Label);

/// <summary>
/// Copied and Skipped (already on the PC) hold ids whose file is now on the PC, whole and at the
/// phone's size. Matched: ids whose PC file was also read back from the phone and matched byte for
/// byte (a Move only). NotTried: files left alone because the copy stopped early. Stopped: the phone
/// sent a file wrongly, so the copy stopped and more copies are refused until the phone is reconnected.
/// </summary>
public sealed record CopyResult(HashSet<int> Copied, HashSet<int> Skipped, HashSet<int> Matched, long Bytes, List<string> Failed, bool Cancelled, int NotTried, bool Stopped)
{
    public bool Verified(int id) => Copied.Contains(id) || Skipped.Contains(id);

    /// <summary>Whether the phone's file may be deleted: its PC copy matched the phone's byte for byte.</summary>
    public bool SafeToDelete(int id) => Matched.Contains(id);

    /// <summary>
    /// The items a Move may delete from the phone: those whose every file (original, edits, Live Photo
    /// clip, edit data) matched. An item is never half-deleted, so a companion that didn't keeps the whole item.
    /// </summary>
    public List<MediaEntry> SafeToDelete(IEnumerable<MediaEntry> entries) => entries.Where(e => e.AllFiles.All(f => SafeToDelete(f.Id))).ToList();
}

public sealed record DeleteResult(List<int> Deleted, List<string> Failed, bool Cancelled);

/// <summary>A connected phone. Id is the shell parsing name: only for telling devices apart, never for rebuilding items.</summary>
public sealed record PhoneDevice(string Id, string Name, bool IsApple);

/// <summary>
/// Owns every shell object for the phone on one dedicated STA thread. Shell/WPD objects are
/// apartment-bound, so all phone access is queued here; the UI only ever sees plain data and bitmaps.
/// Callbacks run on this worker thread.
/// </summary>
public sealed class PhoneWorker : IDisposable
{
    readonly Thread thread;
    readonly object gate = new();
    readonly Queue<Action> queue = new();
    readonly AutoResetEvent signal = new(false);
    readonly Dictionary<int, IShellItem> items = new();
    /// <summary>Per id: a key that names the same phone file across scans (device, folder, name, size), for the cache.</summary>
    readonly Dictionary<int, string> keys = new();
    string deviceName = "";
    List<int> wantedThumbs = new();
    int wantedSize;
    int nextId = 1;
    volatile bool stopping;
    /// <summary>The copy rules, and whether the phone is in trouble (refusing copies and fetches until the next scan).</summary>
    readonly CopyEngine engine = new();

    public volatile bool CancelRequested;

    /// <summary>The app cache: thumbnails and fetched files are kept there. Set once at startup.</summary>
    public AppCache? Cache { get; init; }

    /// <summary>Raised on the worker thread; bitmap is null when the shell has no thumbnail.</summary>
    public event Action<int, Bitmap?>? ThumbnailReady;

    public PhoneWorker()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "Phone worker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    void Post(Action action)
    {
        lock (gate) queue.Enqueue(action);
        signal.Set();
    }

    /// <summary>Replaces the pending thumbnail requests; ids no longer on screen are simply dropped.</summary>
    public void SetWantedThumbnails(List<int> ids, int px)
    {
        lock (gate) { wantedThumbs = ids; wantedSize = px; }
        signal.Set();
    }

    void Run()
    {
        while (!stopping)
        {
            Action? action = null;
            int id = -1, px = 0;
            lock (gate)
            {
                if (queue.Count > 0) action = queue.Dequeue();
                else if (wantedThumbs.Count > 0)
                {
                    id = wantedThumbs[0];
                    wantedThumbs.RemoveAt(0);
                    px = wantedSize;
                }
            }
            if (action != null)
            {
                try { action(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
            else if (id >= 0)
            {
                ThumbnailReady?.Invoke(id, Thumbnail(id, px));
            }
            else signal.WaitOne();
        }
    }

    /// <summary>A thumbnail from the cache, or made by the shell (and then cached). Null when the shell has none.</summary>
    Bitmap? Thumbnail(int id, int px)
    {
        if (!items.TryGetValue(id, out var item)) return null;
        string? key = keys.TryGetValue(id, out var k) ? $"thumb|{k}|{px}" : null;
        if (key != null && Cache?.Get(key) is { } cached)
        {
            try
            {
                // Read through memory so the cached file isn't kept locked by GDI+.
                using var ms = new MemoryStream(File.ReadAllBytes(cached));
                using var img = Image.FromStream(ms);
                return new Bitmap(img);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { Cache.Remove(key); }
        }
        Bitmap? bmp = null;
        try { bmp = Shell.Thumbnail(item, px); }
        catch (Exception ex) when (ex is COMException or InvalidCastException) { }
        if (bmp != null && key != null)
            Cache?.Put(key, "thumb.jpg", path => bmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg));
        return bmp;
    }

    /// <summary>The cache key for a phone file, stable across scans.</summary>
    public static string FileKey(string device, MediaFile f) => $"{device}|{f.Folder}|{f.Name}|{f.Size}";

    // Every WPD device (phones, cameras) has this device interface in its parsing name.
    const string WpdInterface = "{6ac27878-a6fa-4155-ba85-f98f491d4f33}";

    static List<(IShellItem Item, PhoneDevice Info)> FindDevices()
    {
        var found = new List<(IShellItem, PhoneDevice)>();
        foreach (var child in Shell.Children(Shell.ThisPC()))
        {
            string parsing, display;
            try
            {
                parsing = child.GetDisplayName(SIGDN.DESKTOPABSOLUTEPARSING);
                display = Shell.Name(child);
            }
            catch (COMException) { continue; }
            // 05AC is Apple's USB vendor id; the name check covers anything else unusual.
            bool apple = parsing.Contains("vid_05ac", StringComparison.OrdinalIgnoreCase) ||
                         display.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                         display.Contains("iPad", StringComparison.OrdinalIgnoreCase);
            if (apple ||
                parsing.Contains(WpdInterface, StringComparison.OrdinalIgnoreCase) ||
                parsing.Contains(@"\\?\usb#", StringComparison.OrdinalIgnoreCase))
                found.Add((child, new PhoneDevice(parsing, display, apple)));
        }
        return found;
    }

    public void ListDevices(Action<List<PhoneDevice>> done) => Post(() =>
    {
        var list = new List<PhoneDevice>();
        try { list = FindDevices().Select(d => d.Info).ToList(); }
        catch (COMException) { }
        done(list);
    });

    /// <summary>
    /// Lists the photos and videos on a phone: the one with this id, or the first one found. opened names
    /// the phone first; then each folder's files are reported as soon as they are read.
    /// done(device, error): device is null when no phone is connected, error is null on success.
    /// </summary>
    public void Scan(string? deviceId, Action<PhoneDevice> opened, Action<string> status, Action<List<MediaFile>> batch, Action<PhoneDevice?, string?> done) => Post(() =>
    {
        items.Clear();
        keys.Clear();
        engine.Trouble = null;
        lock (gate) wantedThumbs = new();
        IShellItem deviceItem;
        PhoneDevice device;
        try
        {
            var all = FindDevices();
            if (all.Count == 0) { done(null, null); return; }
            (deviceItem, device) = all.FirstOrDefault(d => d.Info.Id == deviceId, all[0]);
        }
        catch (COMException ex) { done(null, "Could not read the list of devices: " + ex.Message); return; }

        try
        {
            deviceName = device.Name;
            opened(device);
            status($"Opening {device.Name}…");
            var storages = Shell.Children(deviceItem).ToList();
            if (storages.Count == 0)
            {
                done(device, device.IsApple
                    ? "The iPhone is connected but its storage is not visible.\n" +
                      "Unlock the phone and tap \"Trust\" if asked, then press Refresh."
                    : "The phone is connected but its storage is not visible.\n" +
                      "Unlock the phone, tap the USB notification and choose \"File transfer\", then press Refresh.");
                return;
            }
            var folders = new List<(IShellItem Item, string Name)>();
            if (device.IsApple)
            {
                // The iPhone's storage holds only its YYYYMM_a folders (and sometimes loose files).
                var rootFiles = new List<MediaFile>();
                foreach (var child in Shell.Children(storages[0]))
                {
                    if (Shell.IsFolder(child)) folders.Add((child, Shell.Name(child)));
                    else rootFiles.Add(Describe(child, ""));
                }
                if (rootFiles.Count > 0) batch(rootFiles);
                // Newest folders first, so recent photos show up straight away.
                folders.Sort((a, b) => string.CompareOrdinal(b.Name, a.Name));
            }
            else
            {
                // Android storage holds the whole file system: read only the photo folders. The first
                // storage (internal) is unprefixed; others (SD card) are named so their folders stay apart.
                for (int s = 0; s < storages.Count; s++)
                {
                    var prefix = s == 0 ? "" : Shell.Name(storages[s]) + "/";
                    foreach (var child in Shell.Children(storages[s]))
                    {
                        if (!Shell.IsFolder(child)) continue;
                        var name = Shell.Name(child);
                        if (name.Equals("DCIM", StringComparison.OrdinalIgnoreCase) || name.Equals("Pictures", StringComparison.OrdinalIgnoreCase))
                            folders.Add((child, prefix + name));
                    }
                }
                // A locked phone can list its storage but nothing in it.
                if (folders.Count == 0)
                {
                    done(device, "The phone is connected but its photo folders are not visible.\n" +
                                 "Unlock the phone, tap the USB notification and choose \"File transfer\", then press Refresh.");
                    return;
                }
            }
            for (int i = 0; i < folders.Count; i++)
            {
                status($"Reading {device.Name}: folder {i + 1} of {folders.Count}…");
                var found = new List<MediaFile>();
                Walk(folders[i].Item, folders[i].Name, found);
                batch(found);
            }
            done(device, null);
        }
        catch (COMException ex)
        {
            done(device, "Lost contact with the phone while reading it (" + ex.Message.Trim() + ").\n" +
                         "Check the cable, unlock the phone and press Refresh.");
        }
    });

    /// <summary>Names starting with "." are Android's hidden files: .thumbnails, .trashed-…, .pending-…, .nomedia.</summary>
    static bool Hidden(IShellItem item) => Shell.Name(item).StartsWith('.');

    void Walk(IShellItem folder, string folderName, List<MediaFile> found)
    {
        foreach (var child in Shell.Children(folder))
        {
            if (Hidden(child)) continue;
            if (Shell.IsFolder(child)) Walk(child, folderName + "/" + Shell.Name(child), found);
            else found.Add(Describe(child, folderName));
        }
    }

    MediaFile Describe(IShellItem item, string folder)
    {
        int id = nextId++;
        items[id] = item;
        var name = Shell.Name(item);
        var date = Shell.Date(item, Shell.PKEY_DateCreated)
                   ?? Library.NameDate(name)
                   ?? Shell.Date(item, Shell.PKEY_DateModified)
                   ?? Library.FolderDate(folder)
                   ?? DateTime.MinValue;
        var kind = Library.KindOf(Path.GetExtension(name).TrimStart('.').ToUpperInvariant(), folder, name);
        var file = new MediaFile(id, name, folder, Shell.Size(item), date, kind == MediaKind.Video ? Shell.Duration(item) : null);
        keys[id] = FileKey(deviceName, file);
        return file;
    }

    /// <summary>
    /// Copies files to the PC (see CopyEngine.Run). readBack (a Move): also compare each PC file with the
    /// phone's, byte for byte, so the result says which phone files are safe to delete.
    /// </summary>
    public void Copy(List<CopyRequest> requests, string destination, bool readBack, Action<int, int, string> progress, Action<CopyResult> done) => Post(() =>
        done(engine.Run(new Files(items), requests, destination, readBack, () => CancelRequested, progress)));

    /// <summary>
    /// Before a Delete: which of these phone files have a PC copy that reads back the same as the phone's
    /// (see CopyEngine.Confirm). done(confirmed ids, stopped because the phone sent data wrongly).
    /// </summary>
    public void Confirm(List<(int Id, string Path, string Label)> claims, Action<int, int, string> progress, Action<HashSet<int>, bool> done) => Post(() =>
    {
        var (confirmed, stopped) = engine.Confirm(new Files(items), claims, () => CancelRequested, progress);
        done(confirmed, stopped);
    });

    /// <summary>The phone's files, by id, for the copy engine. Used only on the worker thread.</summary>
    sealed class Files(Dictionary<int, IShellItem> items) : IPhoneFiles
    {
        IShellItem Item(int id) => items.TryGetValue(id, out var item) ? item : throw new IOException("the phone was rescanned or disconnected");
        public string? CopyTo(int id, string dir, string? newName) => Shell.Copy(Item(id), dir, newName);
        public Stream OpenRead(int id) => Shell.OpenRead(Item(id));
    }

    /// <summary>
    /// Deletes files from the phone, one at a time. Each group is one item's files (original, edits,
    /// Live Photo clip, sidecar): if any file of a group can't be deleted, the rest of that group is
    /// left alone so an item is never half-deleted. Not undoable.
    /// </summary>
    public void Delete(List<MediaEntry> entries, Action<int, int, string> progress, Action<DeleteResult> done) => Post(() =>
    {
        var deleted = new List<int>();
        var failed = new List<string>();
        int total = entries.Sum(e => e.AllFiles.Count()), n = 0;
        foreach (var entry in entries)
        {
            if (CancelRequested) { done(new DeleteResult(deleted, failed, true)); return; }
            var group = entry.AllFiles.ToList();
            // Check the whole group first, so a refusal doesn't leave an item half-deleted.
            var refused = group.FirstOrDefault(f => !items.TryGetValue(f.Id, out var it) || !SafeCanDelete(it));
            if (refused != null)
            {
                failed.Add(entry.PartName(refused) is { } part
                    ? $"{entry.Primary.Name}: the phone doesn't allow deleting its {part}"
                    : $"{entry.Primary.Name}: the phone doesn't allow deleting it");
                n += group.Count;
                continue;
            }
            // Companions first, the original last: if something fails part-way the original survives.
            foreach (var f in group.Skip(1).Append(group[0]))
            {
                progress(n++, total, entry.Primary.Name);
                try
                {
                    Shell.Delete(items[f.Id]);
                    items.Remove(f.Id);
                    deleted.Add(f.Id);
                }
                catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
                {
                    failed.Add($"{entry.Describe(f)}: {ex.Message.Trim()}");
                    break;
                }
            }
        }
        done(new DeleteResult(deleted, failed, false));
    });

    static bool SafeCanDelete(IShellItem item)
    {
        try { return Shell.CanDelete(item); }
        catch (COMException) { return false; }
    }

    /// <summary>
    /// Gets a local copy of a phone file, from the app cache or copied into it, to open it or compare it.
    /// done(path, error): the cached file's path, or null and why not.
    /// </summary>
    public void Fetch(MediaFile file, Action<string?, string?> done) => Post(() =>
    {
        try
        {
            if (!items.TryGetValue(file.Id, out var item) || !keys.TryGetValue(file.Id, out var key))
                throw new IOException("the phone was rescanned or disconnected");
            key = "file|" + key;
            if (Cache?.Get(key) is { } cached)
            {
                // Files cached before checks existed may hold another file's data: drop those.
                if (new FileInfo(cached).Length == file.Size && FileCheck.Problem(cached) == null) { done(cached, null); return; }
                Cache.Remove(key);
            }
            if (engine.Trouble is { } trouble) throw new IOException(trouble);
            var dir = Cache?.Stage() ?? throw new IOException("the cache folder can't be used; check it in Settings");
            string? path;
            try
            {
                string staged;
                try
                {
                    staged = Shell.Copy(item, dir, null) ?? Path.Combine(dir, file.Name);
                    if (!File.Exists(staged) || new FileInfo(staged).Length != file.Size)
                        throw new IOException("the copy doesn't match the phone's file size");
                    if (FileCheck.Problem(staged) is { } problem) throw new IOException(problem);
                }
                catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
                {
                    engine.Trouble = CopyEngine.TroubleText;
                    throw;
                }
                path = Cache.Commit(key, staged) ?? throw new IOException("the file is bigger than the cache allows; raise the limit in Settings");
            }
            finally
            {
                // Commit removes the staged folder; after a failed or partial copy, remove it here.
                Cache.Discard(dir);
            }
            done(path, null);
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
        {
            done(null, ex.Message.Trim());
        }
    });

    public void Dispose()
    {
        stopping = true;
        signal.Set();
    }
}
