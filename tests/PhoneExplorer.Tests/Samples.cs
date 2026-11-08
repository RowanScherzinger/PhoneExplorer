using System.Buffers.Binary;
using System.Text;

namespace PhoneExplorer.Tests;

/// <summary>
/// Small files with the real structure of the formats phones produce. The payload is random (from a seed),
/// so two samples with different seeds never share their data, as two different photos wouldn't.
/// </summary>
public static class Samples
{
    public static byte[] Heic(int seed, int payload = 40_000) =>
        Concat(Box("ftyp", Ascii("heic\0\0\0\0mif1heic")), Box("meta", Random(seed, 300)), Box("mdat", Random(seed + 1, payload)));

    public static byte[] Mov(int seed, int payload = 60_000) =>
        Concat(Box("ftyp", Ascii("qt  \0\0\0\0qt  ")), Box("wide", []), Box("mdat", Random(seed, payload)), Box("moov", Random(seed + 1, 500)));

    /// <summary>A Samsung video: the boxes, then Samsung's own data ending in "SEFT".</summary>
    public static byte[] SamsungMp4(int seed) => Concat(Mov(seed), Random(seed + 2, 700), Ascii("SEFH....SEFT"));

    /// <summary>A baseline JPEG: segments, one scan of entropy-coded data with byte stuffing, end marker.</summary>
    public static byte[] Jpeg(int seed, int scan = 30_000)
    {
        var data = Random(seed, scan);
        var stuffed = new List<byte>();
        foreach (var b in data) { stuffed.Add(b); if (b == 0xFF) stuffed.Add(0x00); }
        return Concat(
            [0xFF, 0xD8],
            Segment(0xE1, Concat(Ascii("Exif\0\0"), Random(seed + 1, 200))),
            Segment(0xDB, Random(seed + 2, 65)),
            Segment(0xC0, Random(seed + 3, 15)),
            Segment(0xDA, Random(seed + 4, 10)),
            stuffed.ToArray(),
            [0xFF, 0xD9]);
    }

    /// <summary>An iPhone JPEG: the photo, then its HDR gain map as a second JPEG straight after it.</summary>
    public static byte[] JpegWithGainMap(int seed) => Concat(Jpeg(seed), Jpeg(seed + 10, 8_000));

    public static byte[] Png(int seed) =>
        Concat([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A],
               Chunk("IHDR", Random(seed, 13)), Chunk("IDAT", Random(seed + 1, 20_000)), Chunk("IEND", []));

    /// <summary>An .AAE edit file (an Apple property list).</summary>
    public static byte[] Aae(int seed) => Ascii(
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
        $"<plist version=\"1.0\">\n<dict>\n\t<key>adjustmentData</key>\n\t<data>{Convert.ToBase64String(Random(seed, 300))}</data>\n</dict>\n</plist>\n");

    /// <summary>Every format, by a name for test output.</summary>
    public static IEnumerable<object[]> All() => new (string, Func<int, byte[]>)[]
    {
        ("HEIC", s => Heic(s)), ("MOV", s => Mov(s)), ("Samsung MP4", SamsungMp4), ("JPEG", s => Jpeg(s)),
        ("JPEG with gain map", JpegWithGainMap), ("PNG", Png), ("AAE", Aae),
    }.Select(x => new object[] { x.Item1 });

    public static byte[] Make(string format, int seed) => format switch
    {
        "HEIC" => Heic(seed),
        "MOV" => Mov(seed),
        "Samsung MP4" => SamsungMp4(seed),
        "JPEG" => Jpeg(seed),
        "JPEG with gain map" => JpegWithGainMap(seed),
        "PNG" => Png(seed),
        "AAE" => Aae(seed),
        _ => throw new ArgumentException(format),
    };

    /// <summary>What an out-of-step phone sends: other's data cut or zero-padded to size.</summary>
    public static byte[] Resized(byte[] other, long size)
    {
        var b = new byte[size];
        Array.Copy(other, b, Math.Min(other.Length, size));
        return b;
    }

    public static byte[] Random(int seed, int length)
    {
        var b = new byte[length];
        new Random(seed).NextBytes(b);
        return b;
    }

    static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

    static byte[] Box(string type, byte[] body)
    {
        var b = new byte[8 + body.Length];
        BinaryPrimitives.WriteUInt32BigEndian(b, (uint)b.Length);
        Encoding.ASCII.GetBytes(type, b.AsSpan(4));
        body.CopyTo(b, 8);
        return b;
    }

    static byte[] Segment(byte marker, byte[] body)
    {
        var b = new byte[4 + body.Length];
        b[0] = 0xFF; b[1] = marker;
        BinaryPrimitives.WriteUInt16BigEndian(b.AsSpan(2), (ushort)(body.Length + 2));
        body.CopyTo(b, 4);
        return b;
    }

    static byte[] Chunk(string type, byte[] body)
    {
        var b = new byte[12 + body.Length];
        BinaryPrimitives.WriteUInt32BigEndian(b, (uint)body.Length);
        Encoding.ASCII.GetBytes(type, b.AsSpan(4));
        body.CopyTo(b, 8);
        return b; // CRC left zero: FileCheck checks structure, not CRCs
    }
}

/// <summary>A folder under the temp folder, removed afterwards.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PhoneExplorer.Tests", Guid.NewGuid().ToString("N"));
    public TempDir() => Directory.CreateDirectory(Path);
    public string Write(string name, byte[] data)
    {
        var p = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(p)!);
        File.WriteAllBytes(p, data);
        return p;
    }
    public void Dispose() { try { Directory.Delete(Path, true); } catch (IOException) { } }
}
