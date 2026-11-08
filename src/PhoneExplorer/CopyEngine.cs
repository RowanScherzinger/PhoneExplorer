using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace PhoneExplorer;

/// <summary>The phone side of a copy. PhoneWorker implements it over WPD; tests use a fake phone.</summary>
public interface IPhoneFiles
{
    /// <summary>
    /// Copies the file into dir under newName (null: its own name). Returns the path written, or null if
    /// unknown. Throws IOException or COMException when the copy fails.
    /// </summary>
    string? CopyTo(int id, string dir, string? newName);

    /// <summary>Opens the file on the phone for reading.</summary>
    Stream OpenRead(int id);
}

/// <summary>Reading a file back from the phone failed: a phone problem, not a PC one.</summary>
public sealed class PhoneReadException(string message, Exception? inner = null) : IOException(message, inner);

/// <summary>
/// The rules for copying files off a phone. Never trust a copy by its size: after one failed transfer an
/// iPhone can stay out of step and send each file under the right name with another file's data, cut to
/// the size asked for (this destroyed ~160 photos on 2026-11-08). So a copy only counts when it is
/// whole (FileCheck), and a Move only deletes a file whose PC copy matched the phone byte for byte.
/// </summary>
public sealed class CopyEngine
{
    public const string TroubleText = "An earlier file didn't come off the phone correctly, and after that the phone can send the wrong data. " +
                                      "Unplug the phone, plug it back in and press Refresh, then try again.";

    /// <summary>Set when the phone sent a file wrongly; copies and fetches are refused until it's cleared (by a rescan).</summary>
    public string? Trouble { get; set; }

    const int HeadBytes = 64 << 10;
    const long ProbeMax = 8 << 20;

    /// <summary>
    /// Copies requests to destination, in order. Stops at the first file the phone sends wrongly: the
    /// files after it could carry the wrong data. With readBack (a Move), each file on the PC, newly copied
    /// or already there, is compared byte for byte with the phone's, and only those go in Matched.
    /// </summary>
    public CopyResult Run(IPhoneFiles phone, IReadOnlyList<CopyRequest> requests, string destination, bool readBack,
                          Func<bool> cancelRequested, Action<int, int, string> progress)
    {
        var copied = new HashSet<int>();
        var skipped = new HashSet<int>();
        var matched = new HashSet<int>();
        long bytes = 0;
        var failed = new List<string>();
        // The start of each file copied in this run: the phone repeating one is the out-of-step failure.
        var heads = new Dictionary<string, string>();
        var probe = new Probe();
        CopyResult Result(bool cancelled, int notTried, bool stopped) => new(copied, skipped, matched, bytes, failed, cancelled, notTried, stopped);

        // Refused: Stopped with nothing tried and nothing in Failed.
        if (Trouble != null) return Result(false, requests.Count, true);
        // A Move goes smallest first: the probes and the extra reads while there are few of them are then small files.
        if (readBack) requests = requests.OrderBy(q => q.Size).ToList();
        for (int i = 0; i < requests.Count; i++)
        {
            if (cancelRequested()) return Result(true, requests.Count - i, false);
            var r = requests[i];
            progress(i, requests.Count, r.Item);
            var dir = Path.Combine(destination, r.RelativeDir);
            string? written = null, expected = null;
            try
            {
                string? found, newName;
                try
                {
                    found = AlreadyThere(dir, r.Name, r.Size, readBack ? path => Same(phone, r.Id, path) : null, out newName);
                    if (found == null) Directory.CreateDirectory(dir);
                }
                catch (Exception ex) when (ex is not PhoneReadException && ex is IOException or UnauthorizedAccessException)
                {
                    // A problem on the PC side (permissions, path): the phone isn't involved, so carry on.
                    failed.Add($"{r.Label}: {ex.Message.Trim()}");
                    continue;
                }

                string onPc;
                if (found != null)
                {
                    onPc = found;
                    heads.TryAdd(Head(found), r.Label);
                }
                else
                {
                    // AlreadyThere picked a name no file has, so anything at it afterwards was written by this copy.
                    expected = Path.Combine(dir, newName ?? r.Name);
                    written = phone.CopyTo(r.Id, dir, newName) ?? expected;
                    if (!File.Exists(written) || new FileInfo(written).Length != r.Size)
                        throw new IOException("the copy on this PC doesn't match the phone's file size");
                    if (FileCheck.Problem(written) is { } problem) throw new IOException(problem);
                    var head = Head(written);
                    if (heads.TryGetValue(head, out var earlier))
                    {
                        // The same start as a file before it: either a real duplicate photo, or the phone sending
                        // that file's data again. A Move can't be sure which, so it leaves this file on the phone.
                        if (readBack)
                        {
                            TryDelete(written);
                            failed.Add($"{r.Label}: it's identical to {earlier}, so it was left on the phone to be safe");
                            continue;
                        }
                        // A copy keeps the phone's file anyway: reading it back tells a duplicate from the failure.
                        if (!Same(phone, r.Id, written)) throw new IOException($"it holds the same data as {earlier}");
                    }
                    else if (readBack && !Same(phone, r.Id, written))
                        throw new IOException("it doesn't match the phone's file when read again");
                    heads.TryAdd(head, r.Label);
                    onPc = written;
                }

                if (readBack)
                {
                    if (!probe.InStep(phone, r.Id, onPc)) throw new IOException("the phone started sending the wrong data, so this file couldn't be checked");
                    matched.Add(r.Id);
                    probe.Offer(r.Id, onPc);
                }
                if (found != null) skipped.Add(r.Id);
                else
                {
                    copied.Add(r.Id);
                    bytes += r.Size;
                }
            }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
            {
                // Don't leave a broken file behind under the photo's name.
                foreach (var path in new[] { written, expected }.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
                    TryDelete(path);
                Trouble = TroubleText;
                failed.Add($"{r.Label}: {ex.Message.Trim()}");
                return Result(false, requests.Count - i - 1, true);
            }
        }
        return Result(false, 0, false);
    }

    /// <summary>
    /// Before a Delete: which phone files really have a copy on this PC. Each claim is a phone file and
    /// a PC file said to be its copy (by Find on PC, or in the copy folder); a file is confirmed only
    /// when the PC file reads back the same as the phone's, byte for byte, with the probe still right
    /// afterwards. Stopped: the phone sent data wrongly (or was already in trouble), so checking stopped.
    /// </summary>
    public (HashSet<int> Confirmed, bool Stopped) Confirm(IPhoneFiles phone, IReadOnlyList<(int Id, string Path, string Label)> claims,
                                                         Func<bool> cancelRequested, Action<int, int, string> progress)
    {
        var confirmed = new HashSet<int>();
        if (Trouble != null) return (confirmed, true);
        var probe = new Probe();
        // Smallest first: the probes and the extra reads while there are few of them are then small files.
        claims = claims.OrderBy(c => SizeOf(c.Path)).ToList();
        for (int i = 0; i < claims.Count; i++)
        {
            if (cancelRequested()) return (confirmed, false);
            var (id, path, label) = claims[i];
            if (confirmed.Contains(id)) continue;
            progress(i, claims.Count, label);
            try
            {
                if (!File.Exists(path) || !Same(phone, id, path)) continue; // not this file's copy: it doesn't count
                if (!probe.InStep(phone, id, path)) { Trouble = TroubleText; return (confirmed, true); }
                confirmed.Add(id);
                probe.Offer(id, path);
            }
            catch (PhoneReadException) { Trouble = TroubleText; return (confirmed, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // can't read the PC file: it doesn't count
        }
        return (confirmed, false);
    }

    /// <summary>
    /// Small files already confirmed, with their bytes. An out-of-step phone sends the data of a file asked
    /// for a few requests before, so reading a probe that wasn't asked for recently can't come back right:
    /// a file and its read-back can agree while both are wrong, but the probe read after them can't. The
    /// probes are read in turn, least recently first, so none was asked for in the last few requests.
    /// </summary>
    sealed class Probe
    {
        const int Pool = 4;
        readonly List<(int Id, byte[] Data)> known = new();

        /// <summary>
        /// Whether the phone is still sending the right data. Until there are enough probes (the first few
        /// files), every probe is read and then the file just checked is read twice more: after two reads of
        /// it in a row, a phone up to two steps behind has to send its own data.
        /// </summary>
        public bool InStep(IPhoneFiles phone, int id, string path)
        {
            if (known.Count < Pool)
                return known.ToList().All(p => Same(phone, p.Id, p.Data)) && Same(phone, id, path) && Same(phone, id, path);
            var next = known[0];
            known.RemoveAt(0);
            known.Add(next);
            return Same(phone, next.Id, next.Data);
        }

        /// <summary>A file that just checked out joins the probes if it's small enough to read often.</summary>
        public void Offer(int id, string path)
        {
            if (known.Count >= Pool || known.Any(p => p.Id == id)) return;
            if (new FileInfo(path).Length <= ProbeMax) known.Add((id, File.ReadAllBytes(path)));
        }
    }

    /// <summary>
    /// Whether this file is already in dir: under its own name or a numbered one an earlier copy used,
    /// whole, at the phone's size and (when same is given) the same as the phone's. Returns its path, or
    /// null with freeName the name to copy it to (null for its own).
    /// </summary>
    public static string? AlreadyThere(string dir, string name, long size, Func<string, bool>? same, out string? freeName)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (int n = 1; ; n++)
        {
            var candidate = n == 1 ? name : $"{stem} ({n}){ext}";
            var path = Path.Combine(dir, candidate);
            if (!File.Exists(path)) { freeName = n == 1 ? null : candidate; return null; }
            // A file of the right size can still hold another file's data: only a whole one counts.
            if (new FileInfo(path).Length == size && FileCheck.Problem(path) == null && (same == null || same(path)))
            {
                freeName = null;
                return path;
            }
        }
    }

    /// <summary>Reads the file from the phone and compares it with the one at path, byte for byte.</summary>
    public static bool Same(IPhoneFiles phone, int id, string path)
    {
        using var local = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
        return Same(phone, id, local);
    }

    /// <summary>Reads the file from the phone and compares it with these bytes.</summary>
    public static bool Same(IPhoneFiles phone, int id, byte[] data)
    {
        using var local = new MemoryStream(data, writable: false);
        return Same(phone, id, local);
    }

    static bool Same(IPhoneFiles phone, int id, Stream local)
    {
        Stream remote;
        try { remote = phone.OpenRead(id); }
        catch (Exception ex) when (ex is COMException or IOException or InvalidCastException or NotSupportedException)
        {
            throw new PhoneReadException("it can't be read back from the phone to check it: " + ex.Message.Trim(), ex);
        }
        using (remote)
        {
            var a = new byte[1 << 20];
            var b = new byte[1 << 20];
            while (true)
            {
                int n;
                try { n = remote.ReadAtLeast(a, a.Length, throwOnEndOfStream: false); }
                catch (Exception ex) when (ex is COMException or IOException)
                {
                    throw new PhoneReadException("it can't be read back from the phone to check it: " + ex.Message.Trim(), ex);
                }
                int m = local.ReadAtLeast(b, b.Length, throwOnEndOfStream: false);
                if (n != m || !a.AsSpan(0, n).SequenceEqual(b.AsSpan(0, m))) return false;
                if (n == 0) return true;
            }
        }
    }

    static string Head(string path)
    {
        using var fs = File.OpenRead(path);
        var buffer = new byte[HeadBytes];
        int n = fs.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
        // Without zero padding: an out-of-step phone pads a smaller file's data with zeros to the size asked for.
        return Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, n).TrimEnd((byte)0)));
    }

    static long SizeOf(string path)
    {
        try { return new FileInfo(path).Length; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return long.MaxValue; }
    }

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
