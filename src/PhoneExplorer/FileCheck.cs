using System.Buffers.Binary;
using System.Text;

namespace PhoneExplorer;

/// <summary>
/// Checks that a copied file is whole, not just the right size. An iPhone over USB can get out of
/// step after a failed copy and send each later file's name with the previous file's data, cut or
/// padded to the size asked for. A size check passes on many of those; a structure check doesn't.
/// The format is read from the bytes, not the extension. Formats it doesn't know pass.
/// </summary>
public static class FileCheck
{
    /// <summary>Null when the file looks whole, otherwise what's wrong with it.</summary>
    public static string? Problem(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
            var head = new byte[16];
            int n = fs.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            fs.Position = 0;
            if (n >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF) return Jpeg(fs);
            if (n >= 8 && IsBox(head)) return Boxes(fs);
            if (n >= 8 && head.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A })) return Png(fs);
            if (n >= 5 && Encoding.ASCII.GetString(head, 0, 5) == "<?xml") return Plist(fs);
            return null;
        }
        catch (IOException ex) { return "it can't be read back: " + ex.Message.Trim(); }
        catch (UnauthorizedAccessException ex) { return "it can't be read back: " + ex.Message.Trim(); }
    }

    const string Cut = "the copy on this PC is incomplete or holds another file's data";

    // ISO base media (HEIC, HEIF, AVIF, MOV, MP4, M4V, 3GP): a chain of boxes that ends exactly at the end of the file.
    static readonly string[] FirstBoxes = { "ftyp", "wide", "mdat", "moov", "free", "skip", "pnot" };

    static bool IsBox(byte[] head) => FirstBoxes.Contains(Encoding.ASCII.GetString(head, 4, 4));

    static string? Boxes(FileStream fs)
    {
        long length = fs.Length, at = 0;
        var h = new byte[16];
        bool sawMedia = false;
        while (at < length)
        {
            if (length - at < 8) return Trailer(fs, at) ? null : Cut;
            fs.Position = at;
            fs.ReadExactly(h, 0, 8);
            long size = BinaryPrimitives.ReadUInt32BigEndian(h);
            var type = Encoding.ASCII.GetString(h, 4, 4);
            if (size == 1)
            {
                if (length - at < 16) return Cut;
                fs.ReadExactly(h, 8, 8);
                size = (long)BinaryPrimitives.ReadUInt64BigEndian(h.AsSpan(8));
            }
            else if (size == 0) size = length - at; // the last box runs to the end of the file
            // Box types are four printable characters; zeros here are padding, not a box.
            if (!h.AsSpan(4, 4).ToArray().All(c => c is >= 0x20 and <= 0x7E))
                return sawMedia && Trailer(fs, at) ? null : Cut;
            if (size < 8 || at + size > length)
                return sawMedia && Trailer(fs, at) ? null : Cut;
            if (type is "mdat" or "moov" or "meta") sawMedia = true;
            at += size;
        }
        return sawMedia ? null : Cut;
    }

    /// <summary>Some Android phones append their own data after the last box (Samsung ends it with "SEFT").</summary>
    static bool Trailer(FileStream fs, long at)
    {
        if (fs.Length - at < 4) return false;
        var tail = new byte[4];
        fs.Position = fs.Length - 4;
        fs.ReadExactly(tail);
        return Encoding.ASCII.GetString(tail) == "SEFT";
    }

    // JPEG: walk the segments, then the image data, to the end-of-image marker. Running out of file
    // before it means the copy was cut. Images that follow straight after (iPhone gain maps) are
    // checked the same way; any other data after the end (Samsung motion photos) is allowed.
    static string? Jpeg(FileStream fs)
    {
        using var s = new BufferedStream(fs, 1 << 16);
        while (true)
        {
            if (JpegImage(s) is { } problem) return problem;
            long end = s.Position;
            if (s.ReadByte() != 0xFF || s.ReadByte() != 0xD8 || s.ReadByte() != 0xFF) return null;
            s.Position = end;
        }
    }

    static string? JpegImage(BufferedStream s)
    {
        s.ReadByte(); s.ReadByte(); // FF D8
        while (true)
        {
            int b = s.ReadByte();
            if (b < 0) return Cut;
            if (b != 0xFF) return Cut;
            int marker;
            do marker = s.ReadByte(); while (marker == 0xFF);
            if (marker < 0) return Cut;
            if (marker == 0xD9) return null;
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7)) continue; // markers without a length
            int hi = s.ReadByte(), lo = s.ReadByte();
            if (lo < 0) return Cut;
            int len = (hi << 8) | lo;
            if (len < 2) return Cut;
            if (!Skip(s, len - 2)) return Cut;
            if (marker != 0xDA) continue;
            // Start of scan: image data follows, up to the next marker that isn't stuffing (FF 00) or a restart.
            while (true)
            {
                b = s.ReadByte();
                if (b < 0) return Cut;
                if (b != 0xFF) continue;
                int next = s.ReadByte();
                while (next == 0xFF) next = s.ReadByte();
                if (next < 0) return Cut;
                if (next == 0x00 || next is >= 0xD0 and <= 0xD7) continue;
                if (next == 0xD9) return null;
                // Another segment (progressive JPEGs have several scans): step back into the walk.
                s.Seek(-2, SeekOrigin.Current);
                break;
            }
        }
    }

    static bool Skip(Stream s, long count)
    {
        if (count == 0) return true;
        if (s.Position + count > s.Length) return false;
        s.Seek(count, SeekOrigin.Current);
        return true;
    }

    // PNG: chunks up to IEND.
    static string? Png(FileStream fs)
    {
        long at = 8;
        var h = new byte[8];
        while (at + 12 <= fs.Length)
        {
            fs.Position = at;
            fs.ReadExactly(h);
            long len = BinaryPrimitives.ReadUInt32BigEndian(h);
            if (Encoding.ASCII.GetString(h, 4, 4) == "IEND") return null;
            at += 12 + len;
        }
        return Cut;
    }

    // XML, such as an .AAE edit file (an Apple property list): it must be closed.
    static string? Plist(FileStream fs)
    {
        if (fs.Length > 64 << 20) return null; // not an edit file; nothing to check
        var text = new StreamReader(fs).ReadToEnd().TrimEnd('\0', ' ', '\r', '\n', '\t');
        if (!text.EndsWith('>')) return Cut;
        if (!text.Contains("plist", StringComparison.Ordinal)) return null;
        return text.EndsWith("</plist>", StringComparison.Ordinal) ? null : Cut;
    }
}
