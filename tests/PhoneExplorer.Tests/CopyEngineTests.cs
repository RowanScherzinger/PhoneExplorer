namespace PhoneExplorer.Tests;

/// <summary>
/// The copy rules, against a fake phone. The rule that matters most: a Move may only delete a phone file
/// whose PC copy is byte for byte the phone's file. Anything else loses photos.
/// </summary>
public sealed class CopyEngineTests : IDisposable
{
    readonly TempDir dir = new();
    public void Dispose() => dir.Dispose();

    static readonly string[] Formats = { "HEIC", "MOV", "JPEG", "JPEG with gain map", "PNG", "AAE", "Samsung MP4" };

    /// <summary>A phone with count files of mixed formats; sizes go down (as when sorted by largest), up or vary.</summary>
    static (FakePhone, List<CopyRequest>) Phone(int count, string order = "mixed", int seed = 0)
    {
        var phone = new FakePhone();
        var rng = new Random(seed);
        var files = Enumerable.Range(1, count).Select(id =>
        {
            var format = Formats[rng.Next(Formats.Length)];
            return (id, format, data: Samples.Make(format, seed * 1000 + id * 7));
        }).ToList();
        if (order == "down") files = files.OrderByDescending(f => f.data.Length).ToList();
        if (order == "up") files = files.OrderBy(f => f.data.Length).ToList();
        var requests = files.Select(f => phone.Add(f.id, $"IMG_{f.id:0000}.{Ext(f.format)}", f.data)).ToList();
        return (phone, requests);
    }

    static string Ext(string format) => format switch
    {
        "HEIC" => "HEIC", "MOV" => "MOV", "Samsung MP4" => "MP4", "PNG" => "PNG", "AAE" => "AAE", _ => "JPG",
    };

    CopyResult Run(CopyEngine engine, IPhoneFiles phone, List<CopyRequest> requests, bool readBack, Func<bool>? cancel = null) =>
        engine.Run(phone, requests, dir.Path, readBack, cancel ?? (() => false), (_, _, _) => { });

    byte[]? OnPc(string name) => File.Exists(Path.Combine(dir.Path, name)) ? File.ReadAllBytes(Path.Combine(dir.Path, name)) : null;

    /// <summary>Every id the result says may be deleted from the phone has an exact copy on the PC.</summary>
    void AssertSafeToDeleteMeansIdentical(FakePhone phone, CopyResult r)
    {
        foreach (var id in r.Matched)
        {
            var (name, data) = phone.Files[id];
            var copies = Directory.GetFiles(dir.Path, Path.GetFileNameWithoutExtension(name) + "*" + Path.GetExtension(name));
            Assert.True(copies.Any(c => File.ReadAllBytes(c).AsSpan().SequenceEqual(data)), $"{name} was marked safe to delete, but no copy on the PC matches it");
        }
    }

    /// <summary>No file in the destination is broken (cut, or another file's data under this name).</summary>
    void AssertNoBrokenFilesLeft(FakePhone phone)
    {
        foreach (var path in Directory.GetFiles(dir.Path, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(path);
            var real = phone.Files.Values.Where(f => name.StartsWith(Path.GetFileNameWithoutExtension(f.Name))).Select(f => f.Data);
            Assert.True(real.Any(d => File.ReadAllBytes(path).AsSpan().SequenceEqual(d)), $"{name} on the PC isn't the phone's file");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_healthy_phone_copies_everything_exactly(bool readBack)
    {
        var (phone, requests) = Phone(20);
        var r = Run(new CopyEngine(), phone, requests, readBack);
        Assert.False(r.Stopped);
        Assert.Empty(r.Failed);
        Assert.Equal(20, r.Copied.Count);
        Assert.Equal(readBack ? 20 : 0, r.Matched.Count);
        foreach (var q in requests) Assert.Equal(phone.Files[q.Id].Data, OnPc(q.Name));
    }

    /// <summary>One bad transfer with the structure intact, and the phone otherwise fine: only the read-back sees it.</summary>
    [Fact]
    public void A_move_catches_a_copy_with_changed_bytes()
    {
        var (phone, requests) = Phone(8, "mixed", 4);
        phone.CorruptCopyOf = requests[5].Id;
        var r = Run(new CopyEngine(), phone, requests, readBack: true);
        Assert.DoesNotContain(requests[5].Id, r.Matched);
        AssertSafeToDeleteMeansIdentical(phone, r);
        AssertNoBrokenFilesLeft(phone);
    }

    /// <summary>
    /// The phone was last asked for photo A (say, it was opened), and is two steps behind. B's PC copy is A's
    /// data. Reading B returns A's data, and a lone probe (A) reads back right, so both agree; the re-reads
    /// of B at the start of a check are what catch it.
    /// </summary>
    [Fact]
    public void A_wrong_copy_is_caught_at_the_start_when_the_phone_was_just_asked_for_its_data()
    {
        var phone = new FakePhone { OutOfStep = true, Lag = 2 };
        var a = Samples.Heic(1);
        var b = Samples.Heic(2); // same size: B's wrong copy is A whole
        phone.Add(1, "IMG_0001.HEIC", a);
        phone.Add(2, "IMG_0002.HEIC", b);
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0001.HEIC"), a);
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0002.HEIC"), a);
        phone.Prime(1, 1);
        var (confirmed, _) = Confirm(new CopyEngine(), phone, (1, "IMG_0001.HEIC"), (2, "IMG_0002.HEIC"));
        Assert.DoesNotContain(2, confirmed);
    }

    [Fact]
    public void A_plain_copy_never_marks_anything_safe_to_delete()
    {
        var (phone, requests) = Phone(5);
        var r = Run(new CopyEngine(), phone, requests, readBack: false);
        Assert.All(requests, q => Assert.False(r.SafeToDelete(q.Id)));
    }

    [Fact]
    public void An_aborted_transfer_stops_the_copy_and_leaves_no_broken_file()
    {
        var (phone, requests) = Phone(10, "up"); // a Move goes smallest first, so this is the order it copies in
        phone.AbortAt = requests[4].Id;
        var engine = new CopyEngine();
        var r = Run(engine, phone, requests, readBack: true);

        Assert.True(r.Stopped);
        Assert.Equal(4, r.Copied.Count);
        Assert.Equal(5, r.NotTried);
        Assert.Single(r.Failed);
        Assert.Contains(requests[4].Name, r.Failed[0]);
        Assert.Null(OnPc(requests[4].Name));
        Assert.DoesNotContain(phone.Log, l => l == $"copy {requests[5].Id}");
        AssertNoBrokenFilesLeft(phone);
    }

    [Fact]
    public void After_a_failure_copies_are_refused_until_the_phone_is_rescanned()
    {
        var (phone, requests) = Phone(6);
        phone.AbortAt = requests[2].Id;
        var engine = new CopyEngine();
        Run(engine, phone, requests, readBack: true);
        Assert.NotNull(engine.Trouble);

        int copiesBefore = phone.Log.Count(l => l.StartsWith("copy"));
        var again = Run(engine, phone, requests, readBack: true);
        Assert.True(again.Stopped);
        Assert.Empty(again.Failed);
        Assert.Equal(requests.Count, again.NotTried);
        Assert.Equal(copiesBefore, phone.Log.Count(l => l.StartsWith("copy")));

        engine.Trouble = null; // what a rescan does
        phone.OutOfStep = false;
        phone.AbortAt = null;
        var fresh = Run(engine, phone, requests, readBack: true);
        Assert.False(fresh.Stopped);
        Assert.Equal(requests.Count, fresh.Matched.Count);
        AssertNoBrokenFilesLeft(phone);
    }

    /// <summary>
    /// The phone goes out of step with no error at all, in every way we can model, over many batches.
    /// A Move must never mark a file safe to delete unless its PC copy is exact, and must leave no broken file.
    /// </summary>
    [Theory]
    [MemberData(nameof(OutOfStepCases))]
    public void Out_of_step_phone_never_gets_a_wrong_file_marked_safe_to_delete(string order, int lag, bool readsLagSeparately, int from, int seed)
    {
        var (phone, requests) = Phone(12, order, seed);
        phone.SilentlyFrom = requests[from].Id;
        phone.Lag = lag;
        phone.ReadsLagSeparately = readsLagSeparately;
        var r = Run(new CopyEngine(), phone, requests, readBack: true);

        AssertSafeToDeleteMeansIdentical(phone, r);
        AssertNoBrokenFilesLeft(phone);
    }

    public static IEnumerable<object[]> OutOfStepCases()
    {
        foreach (var order in new[] { "down", "up", "mixed" })
            foreach (var lag in new[] { 1, 2, 3 })
                foreach (var separate in new[] { false, true })
                    foreach (var from in new[] { 1, 6 })
                        for (int seed = 1; seed <= 3; seed++)
                            yield return new object[] { order, lag, separate, from, seed };
    }

    /// <summary>The same, when the transfer that went wrong reported an error first (what actually happened).</summary>
    [Theory]
    [InlineData("down")]
    [InlineData("up")]
    [InlineData("mixed")]
    public void Out_of_step_after_an_abort_never_gets_a_wrong_file_kept(string order)
    {
        for (int seed = 1; seed <= 5; seed++)
        {
            foreach (var f in Directory.GetFiles(dir.Path)) File.Delete(f);
            var (phone, requests) = Phone(12, order, seed);
            phone.AbortAt = requests[3].Id;
            var r = Run(new CopyEngine(), phone, requests, readBack: false);
            AssertNoBrokenFilesLeft(phone);
            Assert.True(r.Stopped);
        }
    }

    /// <summary>A plain copy has no read-back, so FileCheck and the repeat check must catch the wrong files.</summary>
    [Theory]
    [InlineData("down", 1)]
    [InlineData("down", 2)]
    [InlineData("mixed", 1)]
    [InlineData("mixed", 2)]
    public void A_plain_copy_from_an_out_of_step_phone_stops_and_keeps_no_wrong_file(string order, int seed)
    {
        var (phone, requests) = Phone(12, order, seed);
        phone.SilentlyFrom = requests[3].Id;
        var r = Run(new CopyEngine(), phone, requests, readBack: false);
        Assert.True(r.Stopped);
        AssertNoBrokenFilesLeft(phone);
    }

    [Fact]
    public void A_plain_copy_catches_a_file_that_repeats_the_one_before_it()
    {
        // The previous file was smaller and a JPEG, so the wrong file is whole plus zeros and passes FileCheck.
        var phone = new FakePhone();
        var small = Samples.Jpeg(1, 10_000);
        var big = Samples.Jpeg(2, 30_000);
        var requests = new List<CopyRequest> { phone.Add(1, "IMG_0001.JPG", small), phone.Add(2, "IMG_0002.JPG", big) };
        phone.SilentlyFrom = 2;
        var r = Run(new CopyEngine(), phone, requests, readBack: false);
        Assert.True(r.Stopped);
        Assert.DoesNotContain(2, r.Copied);
        Assert.Null(OnPc("IMG_0002.JPG"));
    }

    [Fact]
    public void A_file_already_on_the_PC_with_other_data_is_not_trusted_by_a_move()
    {
        var phone = new FakePhone();
        var real = Samples.Heic(1);
        var other = Samples.Heic(2); // same size, whole, a different photo
        Assert.Equal(real.Length, other.Length);
        var requests = new List<CopyRequest> { phone.Add(1, "IMG_0001.HEIC", real) };
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0001.HEIC"), other);

        var r = Run(new CopyEngine(), phone, requests, readBack: true);
        Assert.Contains(1, r.Matched);
        Assert.Equal(other, OnPc("IMG_0001.HEIC"));       // the existing file is left alone
        Assert.Equal(real, OnPc("IMG_0001 (2).HEIC"));    // and the real one copied beside it
    }

    [Fact]
    public void A_broken_file_already_on_the_PC_is_not_counted_as_copied()
    {
        var phone = new FakePhone();
        var real = Samples.Heic(1);
        var requests = new List<CopyRequest> { phone.Add(1, "IMG_0001.HEIC", real) };
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0001.HEIC"), Samples.Resized(Samples.Heic(5, 60_000), real.Length));

        var r = Run(new CopyEngine(), phone, requests, readBack: false);
        Assert.DoesNotContain(1, r.Skipped);
        Assert.Contains(1, r.Copied);
        Assert.Equal(real, OnPc("IMG_0001 (2).HEIC"));
    }

    [Fact]
    public void A_file_already_on_the_PC_and_identical_is_skipped_and_safe()
    {
        var phone = new FakePhone();
        var real = Samples.Jpeg(1);
        var requests = new List<CopyRequest> { phone.Add(1, "IMG_0001.JPG", real) };
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0001.JPG"), real);

        var r = Run(new CopyEngine(), phone, requests, readBack: true);
        Assert.Contains(1, r.Skipped);
        Assert.Contains(1, r.Matched);
        Assert.DoesNotContain(phone.Log, l => l.StartsWith("copy"));
    }

    [Fact]
    public void Real_duplicate_photos_in_a_move_keep_the_second_on_the_phone()
    {
        var phone = new FakePhone();
        var photo = Samples.Heic(1);
        var requests = new List<CopyRequest> { phone.Add(1, "IMG_0001.HEIC", photo), phone.Add(2, "IMG_0002.HEIC", photo.ToArray()) };
        var r = Run(new CopyEngine(), phone, requests, readBack: true);
        Assert.False(r.Stopped);
        Assert.Contains(1, r.Matched);
        Assert.DoesNotContain(2, r.Matched);
        Assert.Contains(r.Failed, f => f.Contains("IMG_0002.HEIC") && f.Contains("left on the phone"));
    }

    [Fact]
    public void Real_duplicate_photos_in_a_copy_are_both_copied()
    {
        var phone = new FakePhone();
        var photo = Samples.Heic(1);
        var requests = new List<CopyRequest> { phone.Add(1, "IMG_0001.HEIC", photo), phone.Add(2, "IMG_0002.HEIC", photo.ToArray()) };
        var r = Run(new CopyEngine(), phone, requests, readBack: false);
        Assert.Equal(2, r.Copied.Count);
        Assert.Empty(r.Failed);
    }

    [Fact]
    public void A_move_deletes_nothing_when_files_cant_be_read_back()
    {
        var (phone, requests) = Phone(4);
        phone.CantRead = true;
        var r = Run(new CopyEngine(), phone, requests, readBack: true);
        Assert.Empty(r.Matched);
        Assert.True(r.Stopped);
        AssertNoBrokenFilesLeft(phone);
    }

    [Fact]
    public void A_file_of_the_wrong_size_fails_even_if_it_looks_whole()
    {
        var phone = new FakePhone();
        var requests = new List<CopyRequest> { phone.Add(1, "IMG_0001.HEIC", Samples.Heic(1)) };
        var wrong = requests[0] with { Size = requests[0].Size + 10 }; // the phone listed one size and sent another
        var r = Run(new CopyEngine(), phone, [wrong], readBack: false);
        Assert.True(r.Stopped);
        Assert.Null(OnPc("IMG_0001.HEIC"));
    }

    [Fact]
    public void Cancelling_stops_before_the_next_file()
    {
        var (phone, requests) = Phone(6);
        int calls = 0;
        var r = Run(new CopyEngine(), phone, requests, readBack: true, cancel: () => ++calls > 2);
        Assert.True(r.Cancelled);
        Assert.Equal(2, r.Copied.Count);
        Assert.Equal(4, r.NotTried);
    }

    // ---- Confirm: what a Delete may count as already on this PC ----

    (HashSet<int> Confirmed, bool Stopped) Confirm(CopyEngine engine, FakePhone phone, params (int Id, string Name)[] claims) =>
        engine.Confirm(phone, claims.Select(c => (c.Id, Path.Combine(dir.Path, c.Name), c.Name)).ToList(), () => false, (_, _, _) => { });

    [Fact]
    public void Delete_confirms_an_identical_copy()
    {
        var phone = new FakePhone();
        phone.Add(1, "IMG_0001.HEIC", Samples.Heic(1));
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0001.HEIC"), Samples.Heic(1));
        Assert.Contains(1, Confirm(new CopyEngine(), phone, (1, "IMG_0001.HEIC")).Confirmed);
    }

    /// <summary>The 2026-11-08 leftover: a smaller photo's whole JPEG, zero-padded to the right size, passes FileCheck.</summary>
    [Fact]
    public void Delete_does_not_count_another_photos_data_with_the_right_name_and_size()
    {
        var phone = new FakePhone();
        var real = Samples.Jpeg(1, 30_000);
        phone.Add(1, "IMG_0001.JPG", real);
        var leftover = Samples.Resized(Samples.Jpeg(2, 10_000), real.Length);
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0001.JPG"), leftover);
        Assert.Null(FileCheck.Problem(Path.Combine(dir.Path, "IMG_0001.JPG"))); // looks fine on its own
        Assert.Empty(Confirm(new CopyEngine(), phone, (1, "IMG_0001.JPG")).Confirmed);
    }

    [Fact]
    public void Delete_counts_nothing_while_the_phone_is_in_trouble()
    {
        var phone = new FakePhone();
        phone.Add(1, "IMG_0001.HEIC", Samples.Heic(1));
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0001.HEIC"), Samples.Heic(1));
        var r = Confirm(new CopyEngine { Trouble = CopyEngine.TroubleText }, phone, (1, "IMG_0001.HEIC"));
        Assert.True(r.Stopped);
        Assert.Empty(r.Confirmed);
        Assert.Empty(phone.Log);
    }

    [Fact]
    public void Delete_counts_nothing_when_the_phone_cant_be_read()
    {
        var phone = new FakePhone { CantRead = true };
        phone.Add(1, "IMG_0001.HEIC", Samples.Heic(1));
        File.WriteAllBytes(Path.Combine(dir.Path, "IMG_0001.HEIC"), Samples.Heic(1));
        var engine = new CopyEngine();
        var r = Confirm(engine, phone, (1, "IMG_0001.HEIC"));
        Assert.True(r.Stopped);
        Assert.Empty(r.Confirmed);
        Assert.NotNull(engine.Trouble);
    }

    /// <summary>
    /// The PC holds what an out-of-step phone left behind (each file another photo's data, at the right size),
    /// next to some good copies, and the phone is out of step while checking too. Nothing wrong may be confirmed.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConfirmCases))]
    public void Delete_never_confirms_a_wrong_copy_from_an_out_of_step_phone(int lag, bool readsLagSeparately, int seed)
    {
        var (phone, requests) = Phone(12, "mixed", seed);
        var rng = new Random(seed);
        foreach (var (q, k) in requests.Select((q, k) => (q, k)))
        {
            // Every other file on the PC is the one before it, resized; the rest are good copies.
            var data = k % 2 == 1 ? Samples.Resized(phone.Files[requests[k - 1].Id].Data, q.Size) : phone.Files[q.Id].Data;
            File.WriteAllBytes(Path.Combine(dir.Path, q.Name), data);
        }
        phone.OutOfStep = rng.Next(2) == 0;
        phone.SilentlyFrom = phone.OutOfStep ? null : requests[rng.Next(requests.Count)].Id;
        phone.Lag = lag;
        phone.ReadsLagSeparately = readsLagSeparately;
        var engine = new CopyEngine();
        // SilentlyFrom only acts on copies; Confirm only reads, so switch the phone at that file's read instead.
        var (confirmed, _) = engine.Confirm(new SwitchOnRead(phone), requests.Select(q => (q.Id, Path.Combine(dir.Path, q.Name), q.Name)).ToList(), () => false, (_, _, _) => { });
        foreach (var id in confirmed)
            Assert.True(OnPc(phone.Files[id].Name)!.AsSpan().SequenceEqual(phone.Files[id].Data), $"{phone.Files[id].Name} was confirmed but its PC copy is wrong");
    }

    public static IEnumerable<object[]> ConfirmCases()
    {
        foreach (var lag in new[] { 1, 2, 3 })
            foreach (var separate in new[] { false, true })
                for (int seed = 1; seed <= 6; seed++)
                    yield return new object[] { lag, separate, seed };
    }

    /// <summary>
    /// Randomized: many small edit files (the probes), then photos whose PC copies are a mix of good ones
    /// and other files' data at the right size; the phone was asked for random files before and goes out of
    /// step at a random point, 1 to 4 steps behind. Nothing wrong may be confirmed.
    /// </summary>
    [Theory]
    [MemberData(nameof(RandomConfirmCases))]
    public void Delete_never_confirms_a_wrong_copy_randomized(int seed)
    {
        var rng = new Random(seed);
        var phone = new FakePhone { Lag = rng.Next(1, 5), ReadsLagSeparately = rng.Next(2) == 0 };
        int n = 16;
        for (int id = 1; id <= n; id++)
        {
            var format = id <= 6 ? "AAE" : Formats[rng.Next(Formats.Length)];
            phone.Add(id, $"IMG_{id:0000}.{Ext(format)}", Samples.Make(format, seed * 100 + id));
        }
        for (int id = 1; id <= n; id++)
        {
            var data = phone.Files[id].Data;
            if (id > 6 && rng.Next(3) == 0)
                data = Samples.Resized(phone.Files[rng.Next(1, n + 1)].Data, data.Length); // another file's data at this size
            File.WriteAllBytes(Path.Combine(dir.Path, phone.Files[id].Name), data);
        }
        phone.Prime(Enumerable.Range(0, 3).Select(_ => rng.Next(1, n + 1)).ToArray());
        phone.SilentlyFrom = rng.Next(1, n + 1);
        var (confirmed, _) = new CopyEngine().Confirm(new SwitchOnRead(phone),
            phone.Files.Select(kv => (kv.Key, Path.Combine(dir.Path, kv.Value.Name), kv.Value.Name)).ToList(), () => false, (_, _, _) => { });
        foreach (var id in confirmed)
            Assert.True(OnPc(phone.Files[id].Name)!.AsSpan().SequenceEqual(phone.Files[id].Data), $"{phone.Files[id].Name} was confirmed but its PC copy is wrong (lag {phone.Lag})");
    }

    public static IEnumerable<object[]> RandomConfirmCases() => Enumerable.Range(1, 300).Select(s => new object[] { s });

    /// <summary>Randomized Move: some files already on the PC (good or wrong), random earlier requests, out of step at a random point, 1 to 4 steps behind.</summary>
    [Theory]
    [MemberData(nameof(RandomConfirmCases))]
    public void Move_never_marks_a_wrong_file_safe_randomized(int seed)
    {
        var rng = new Random(seed);
        var (phone, requests) = Phone(14, "mixed", seed);
        phone.Lag = rng.Next(1, 5);
        phone.ReadsLagSeparately = rng.Next(2) == 0;
        foreach (var q in requests.Where(_ => rng.Next(4) == 0))
        {
            var other = phone.Files[requests[rng.Next(requests.Count)].Id].Data;
            File.WriteAllBytes(Path.Combine(dir.Path, q.Name), rng.Next(2) == 0 ? phone.Files[q.Id].Data : Samples.Resized(other, q.Size));
        }
        phone.Prime(Enumerable.Range(0, 3).Select(_ => requests[rng.Next(requests.Count)].Id).ToArray());
        if (rng.Next(3) == 0) phone.OutOfStep = true;
        else phone.SilentlyFrom = requests[rng.Next(requests.Count)].Id;
        var r = Run(new CopyEngine(), new SwitchOnRead(phone), requests, readBack: true);
        AssertSafeToDeleteMeansIdentical(phone, r);
    }

    /// <summary>Puts the fake phone out of step at the read of its SilentlyFrom file.</summary>
    sealed class SwitchOnRead(FakePhone phone) : IPhoneFiles
    {
        public string? CopyTo(int id, string dir, string? newName) => phone.CopyTo(id, dir, newName);
        public Stream OpenRead(int id)
        {
            if (id == phone.SilentlyFrom) phone.OutOfStep = true;
            return phone.OpenRead(id);
        }
    }

    [Fact]
    public void A_move_only_deletes_items_whose_every_file_matched()
    {
        MediaFile F(int id, string name) => new(id, name, "202611_a", 100, DateTime.Now, null);
        var photo = new MediaEntry { Primary = F(1, "IMG_0001.HEIC") };
        photo.Extras.Add((F(2, "IMG_0001.MOV"), FileRole.Live));
        photo.Extras.Add((F(3, "IMG_0001.AAE"), FileRole.Sidecar));
        var other = new MediaEntry { Primary = F(4, "IMG_0002.HEIC") };

        var r = new CopyResult(new() { 1, 2, 3, 4 }, new(), Matched: new() { 1, 2, 4 }, 0, new(), false, 0, false);
        Assert.Equal(new[] { other }, r.SafeToDelete(new[] { photo, other }));
    }
}
