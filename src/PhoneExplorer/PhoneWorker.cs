using System.Drawing;
using System.Runtime.InteropServices;

namespace PhoneExplorer;

public sealed record CopyRequest(int Id, string Name, string RelativeDir, long Size);

/// <summary>Verified: ids whose file is now on the PC at the same size (copied or already there).</summary>
public sealed record CopyResult(int Copied, int Skipped, long Bytes, List<string> Failed, bool Cancelled, HashSet<int> Verified);

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
    List<int> wantedThumbs = new();
    int wantedSize;
    int nextId = 1;
    volatile bool stopping;

    public volatile bool CancelRequested;

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
                Bitmap? bmp = null;
                if (items.TryGetValue(id, out var item))
                {
                    try { bmp = Shell.Thumbnail(item, px); }
                    catch (Exception ex) when (ex is COMException or InvalidCastException) { }
                }
                ThumbnailReady?.Invoke(id, bmp);
            }
            else signal.WaitOne();
        }
    }

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
        return new MediaFile(id, name, folder, Shell.Size(item), date, kind == MediaKind.Video ? Shell.Duration(item) : null);
    }

    public void Copy(List<CopyRequest> requests, string destination, Action<int, int, string> progress, Action<CopyResult> done) => Post(() =>
    {
        int copied = 0, skipped = 0;
        long bytes = 0;
        var failed = new List<string>();
        var verified = new HashSet<int>();
        for (int i = 0; i < requests.Count; i++)
        {
            if (CancelRequested) { done(new CopyResult(copied, skipped, bytes, failed, true, verified)); return; }
            var r = requests[i];
            progress(i, requests.Count, r.Name);
            try
            {
                var dir = Path.Combine(destination, r.RelativeDir);
                var target = Path.Combine(dir, r.Name);
                if (File.Exists(target) && new FileInfo(target).Length == r.Size) { skipped++; verified.Add(r.Id); continue; }
                string? newName = null;
                if (File.Exists(target))
                {
                    var stem = Path.GetFileNameWithoutExtension(r.Name);
                    var ext = Path.GetExtension(r.Name);
                    for (int n = 2; File.Exists(Path.Combine(dir, newName = $"{stem} ({n}){ext}")); n++) { }
                }
                if (!items.TryGetValue(r.Id, out var item)) throw new IOException("the phone was rescanned or disconnected");
                Shell.Copy(item, dir, newName);
                var written = Path.Combine(dir, newName ?? r.Name);
                if (!File.Exists(written) || new FileInfo(written).Length != r.Size)
                    throw new IOException("the copy on this PC doesn't match the phone's file size");
                copied++;
                bytes += r.Size;
                verified.Add(r.Id);
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
            {
                failed.Add($"{r.Name}: {ex.Message.Trim()}");
            }
        }
        done(new CopyResult(copied, skipped, bytes, failed, false, verified));
    });

    /// <summary>
    /// Deletes files from the phone, one at a time. Each group is one item's files (original, edits,
    /// Live Photo clip, sidecar): if any file of a group can't be deleted, the rest of that group is
    /// left alone so an item is never half-deleted. Not undoable.
    /// </summary>
    public void Delete(List<List<MediaFile>> groups, Action<int, int, string> progress, Action<DeleteResult> done) => Post(() =>
    {
        var deleted = new List<int>();
        var failed = new List<string>();
        int total = groups.Sum(g => g.Count), n = 0;
        foreach (var group in groups)
        {
            if (CancelRequested) { done(new DeleteResult(deleted, failed, true)); return; }
            // Check the whole group first, so a refusal doesn't leave an item half-deleted.
            var refused = group.FirstOrDefault(f => !items.TryGetValue(f.Id, out var it) || !SafeCanDelete(it));
            if (refused != null)
            {
                failed.Add($"{group[0].Name}: the phone doesn't allow deleting {refused.Name}");
                n += group.Count;
                continue;
            }
            // Companions first, the original last: if something fails part-way the original survives.
            foreach (var f in group.Skip(1).Append(group[0]))
            {
                progress(n++, total, f.Name);
                try
                {
                    Shell.Delete(items[f.Id]);
                    items.Remove(f.Id);
                    deleted.Add(f.Id);
                }
                catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
                {
                    failed.Add($"{f.Name}: {ex.Message.Trim()}");
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

    /// <summary>Copies a file to a temp folder so it can be opened with the default app.</summary>
    public void CopyToTemp(MediaFile file, Action<string?, string?> done) => Post(() =>
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "PhoneExplorer");
            var target = Path.Combine(dir, file.Name);
            if (!(File.Exists(target) && new FileInfo(target).Length == file.Size))
            {
                if (File.Exists(target)) File.Delete(target);
                if (!items.TryGetValue(file.Id, out var item)) throw new IOException("the phone was rescanned or disconnected");
                Shell.Copy(item, dir, null);
            }
            done(target, null);
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
