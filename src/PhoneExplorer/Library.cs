using System.Globalization;
using System.Text.RegularExpressions;

namespace PhoneExplorer;

public enum MediaKind { Photo, Video, Image, Other }

public enum FileRole { Primary, Edited, Live, EditedLive, Sidecar }

/// <summary>One file on the phone. Id refers to the shell item held by the PhoneWorker.</summary>
public sealed record MediaFile(int Id, string Name, string Folder, long Size, DateTime Date, TimeSpan? Duration)
{
    public string Ext => Path.GetExtension(Name).TrimStart('.').ToUpperInvariant();
    public MediaKind Kind => Library.KindOf(Ext, Folder, Name);
}

/// <summary>
/// What the phone shows as one item: the original plus the files that belong to it
/// (iPhone only: edited version IMG_E####, Live Photo motion .MOV, .AAE edit sidecar).
/// </summary>
public sealed class MediaEntry
{
    public required MediaFile Primary { get; init; }
    public List<(MediaFile File, FileRole Role)> Extras { get; } = new();

    public int Key => Primary.Id;
    public MediaKind Kind => Primary.Kind;
    public DateTime Date => Primary.Date;
    public bool IsEdited => Extras.Any(x => x.Role == FileRole.Edited);
    public bool HasLive => Extras.Any(x => x.Role == FileRole.Live);

    /// <summary>The file whose thumbnail we show and which we open: the edit if there is one, as on the phone.</summary>
    public MediaFile Display => Extras.FirstOrDefault(x => x.Role == FileRole.Edited).File ?? Primary;

    public IEnumerable<MediaFile> AllFiles => Extras.Select(x => x.File).Prepend(Primary);
}

public static class Library
{
    static readonly HashSet<string> PhotoExts = new() { "HEIC", "HEIF", "JPG", "JPEG", "DNG", "TIF", "TIFF" };
    static readonly HashSet<string> ImageExts = new() { "PNG", "WEBP", "GIF", "BMP" };
    static readonly HashSet<string> VideoExts = new() { "MOV", "MP4", "M4V", "3GP", "AVI" };

    static readonly Regex EditedName = new(@"^IMG_E(\d+)$", RegexOptions.IgnoreCase);
    static readonly Regex FolderMonth = new(@"^(\d{4})(\d{2})");
    // Android names such as 20261017_123456.jpg, PXL_20261017_123456789.jpg, Screenshot_2026-10-17-12-34-56.png
    static readonly Regex NameStamp = new(@"(?<!\d)(20\d{2})-?(\d{2})-?(\d{2})[ _-]?(\d{2})[-.]?(\d{2})[-.]?(\d{2})");

    public static MediaKind KindOf(string ext) =>
        PhotoExts.Contains(ext) ? MediaKind.Photo :
        VideoExts.Contains(ext) ? MediaKind.Video :
        ImageExts.Contains(ext) ? MediaKind.Image : MediaKind.Other;

    /// <summary>As KindOf(ext), but JPG screenshots (Android saves them as JPG) count as screenshots and images.</summary>
    public static MediaKind KindOf(string ext, string folder, string name)
    {
        var kind = KindOf(ext);
        if (kind == MediaKind.Photo &&
            (name.StartsWith("Screenshot", StringComparison.OrdinalIgnoreCase) ||
             folder.Split('/').Contains("Screenshots", StringComparer.OrdinalIgnoreCase)))
            return MediaKind.Image;
        return kind;
    }

    /// <summary>The date and time in Android file names. Pixel (PXL_) names are in UTC; the others are local.</summary>
    public static DateTime? NameDate(string name)
    {
        var m = NameStamp.Match(name);
        if (!m.Success) return null;
        int[] v = Enumerable.Range(1, 6).Select(i => int.Parse(m.Groups[i].Value)).ToArray();
        if (v[1] is < 1 or > 12 || v[2] is < 1 or > 31 || v[3] > 23 || v[4] > 59 || v[5] > 59) return null;
        if (v[2] > DateTime.DaysInMonth(v[0], v[1])) return null;
        var date = new DateTime(v[0], v[1], v[2], v[3], v[4], v[5]);
        return name.StartsWith("PXL_", StringComparison.OrdinalIgnoreCase)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc).ToLocalTime()
            : date;
    }

    /// <summary>Parses the YYYYMM prefix of iPhone DCIM folder names such as "202610_a".</summary>
    public static DateTime? FolderDate(string folder)
    {
        var m = FolderMonth.Match(folder);
        if (!m.Success) return null;
        int y = int.Parse(m.Groups[1].Value), mo = int.Parse(m.Groups[2].Value);
        return mo is >= 1 and <= 12 ? new DateTime(y, mo, 1) : null;
    }

    public static string MonthTitle(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>Groups files into items. Only iPhone naming is understood; on other phones every file is its own item.</summary>
    public static List<MediaEntry> Build(IEnumerable<MediaFile> files, bool apple)
    {
        if (!apple) return files.Select(f => new MediaEntry { Primary = f }).ToList();
        var result = new List<MediaEntry>();
        foreach (var group in files.GroupBy(f => (f.Folder, Stem(f.Name, out _))))
        {
            var visual = new List<MediaFile>(); var visualEdits = new List<MediaFile>();
            var videos = new List<MediaFile>(); var videoEdits = new List<MediaFile>();
            var other = new List<MediaFile>();
            foreach (var f in group.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                Stem(f.Name, out bool edit);
                switch (f.Kind)
                {
                    case MediaKind.Photo or MediaKind.Image: (edit ? visualEdits : visual).Add(f); break;
                    case MediaKind.Video: (edit ? videoEdits : videos).Add(f); break;
                    default: other.Add(f); break;
                }
            }

            MediaEntry entry;
            if (visual.Count > 0 || visualEdits.Count > 0)
            {
                entry = new MediaEntry { Primary = visual.Count > 0 ? visual[0] : visualEdits[0] };
                // Rare: two originals with the same number (e.g. HEIC + JPG). Show the others separately.
                foreach (var extra in visual.Skip(1)) result.Add(new MediaEntry { Primary = extra });
                foreach (var e in visualEdits.Where(e => e != entry.Primary)) entry.Extras.Add((e, FileRole.Edited));
                foreach (var v in videos) entry.Extras.Add((v, FileRole.Live));
                foreach (var v in videoEdits) entry.Extras.Add((v, FileRole.EditedLive));
            }
            else if (videos.Count > 0 || videoEdits.Count > 0)
            {
                entry = new MediaEntry { Primary = videos.Count > 0 ? videos[0] : videoEdits[0] };
                foreach (var extra in videos.Skip(1)) result.Add(new MediaEntry { Primary = extra });
                foreach (var e in videoEdits.Where(e => e != entry.Primary)) entry.Extras.Add((e, FileRole.Edited));
            }
            else
            {
                entry = new MediaEntry { Primary = other[0] };
                other.RemoveAt(0);
            }
            foreach (var o in other) entry.Extras.Add((o, FileRole.Sidecar));
            result.Add(entry);
        }
        return result;
    }

    /// <summary>"IMG_E1234.JPG" -> "IMG_1234" (edit = true); "IMG_1234.MOV" -> "IMG_1234".</summary>
    static string Stem(string name, out bool edit)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        var m = EditedName.Match(stem);
        edit = m.Success;
        return (edit ? "IMG_" + m.Groups[1].Value : stem).ToUpperInvariant();
    }
}
