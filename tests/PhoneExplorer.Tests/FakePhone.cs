namespace PhoneExplorer.Tests;

/// <summary>
/// A phone for the copy engine that can fail the way the iPhone did on 2026-11-08: one transfer aborts,
/// and from then on each transfer sends the file asked for Lag transfers earlier, cut or zero-padded to
/// the size asked for, under the right name.
/// </summary>
public sealed class FakePhone : IPhoneFiles
{
    public readonly Dictionary<int, (string Name, byte[] Data)> Files = new();

    /// <summary>The copy of this id aborts part-way (leaving half a file), and the phone goes out of step.</summary>
    public int? AbortAt;
    /// <summary>The phone is out of step from this id's copy on, without any error.</summary>
    public int? SilentlyFrom;
    public bool OutOfStep;
    public int Lag = 1;
    /// <summary>Reads (the check after a copy) keep their own lag, so a read can repeat the copy's wrong data.</summary>
    public bool ReadsLagSeparately;
    /// <summary>The phone can't open files for reading.</summary>
    public bool CantRead;
    /// <summary>The copy of this id arrives with some bytes changed (the structure intact), once; the phone stays in step.</summary>
    public int? CorruptCopyOf;

    /// <summary>Requests made before the test starts (thumbnails, an opened photo): what an out-of-step phone can send next.</summary>
    public void Prime(params int[] ids)
    {
        transfers.AddRange(ids);
        reads.AddRange(ids);
    }

    readonly List<int> transfers = new(), reads = new();
    public readonly List<string> Log = new();

    public CopyRequest Add(int id, string name, byte[] data, string dir = "")
    {
        Files[id] = (name, data);
        return new CopyRequest(id, name, dir, data.Length, name, name);
    }

    public string? CopyTo(int id, string dir, string? newName)
    {
        var path = Path.Combine(dir, newName ?? Files[id].Name);
        Log.Add($"copy {id}");
        if (id == AbortAt)
        {
            OutOfStep = true;
            transfers.Add(id);
            File.WriteAllBytes(path, Files[id].Data.AsSpan(0, Files[id].Data.Length / 2).ToArray());
            throw new IOException("the phone stopped sending it");
        }
        if (id == SilentlyFrom) OutOfStep = true;
        var data = Send(id, transfers);
        if (id == CorruptCopyOf)
        {
            data = data.ToArray();
            for (int k = data.Length / 2; k < data.Length / 2 + 64; k++) data[k] ^= 0x5A;
            CorruptCopyOf = null;
        }
        File.WriteAllBytes(path, data);
        return path;
    }

    public Stream OpenRead(int id)
    {
        Log.Add($"read {id}");
        if (CantRead) throw new NotSupportedException("no stream");
        return new MemoryStream(Send(id, ReadsLagSeparately ? reads : transfers));
    }

    byte[] Send(int id, List<int> history)
    {
        history.Add(id);
        if (!OutOfStep || history.Count <= Lag) return Files[id].Data;
        var sent = history[^(1 + Lag)];
        return Samples.Resized(Files[sent].Data, Files[id].Data.Length);
    }
}
