namespace PhoneExplorer.Tests;

/// <summary>FileCheck: a whole file passes; a cut one, or one holding another file's data, doesn't.</summary>
public sealed class FileCheckTests : IDisposable
{
    readonly TempDir dir = new();
    public void Dispose() => dir.Dispose();

    string? Check(byte[] data) => FileCheck.Problem(dir.Write(Guid.NewGuid() + ".bin", data));

    [Theory, MemberData(nameof(Samples.All), MemberType = typeof(Samples))]
    public void Whole_file_passes(string format) => Assert.Null(Check(Samples.Make(format, 1)));

    [Theory, MemberData(nameof(Samples.All), MemberType = typeof(Samples))]
    public void Cut_file_fails(string format)
    {
        var whole = Samples.Make(format, 1);
        foreach (var fraction in new[] { 0.05, 0.3, 0.6, 0.95 })
        {
            var cut = whole[..(int)(whole.Length * fraction)];
            Assert.True(Check(cut) != null, $"{format} cut to {fraction:P0} passed");
        }
    }

    [Theory, MemberData(nameof(Samples.All), MemberType = typeof(Samples))]
    public void Cut_by_a_few_bytes_fails(string format)
    {
        var whole = Samples.Make(format, 1);
        int bytes = format == "AAE" ? 12 : 3; // a plist may end in a newline or two
        Assert.NotNull(Check(whole[..^bytes]));
    }

    /// <summary>The 2026-11-08 failure: a file arrives under its own name and size with the previous, bigger file's data.</summary>
    [Theory, MemberData(nameof(Samples.All), MemberType = typeof(Samples))]
    public void Previous_files_data_cut_to_this_files_size_fails(string format)
    {
        var previous = Samples.Make(format, 1);
        var size = (long)(previous.Length * 0.97);
        Assert.NotNull(Check(Samples.Resized(previous, size)));
    }

    [Theory]
    [InlineData("HEIC")]
    [InlineData("MOV")]
    public void Previous_files_data_padded_to_a_bigger_size_fails(string format)
    {
        var previous = Samples.Make(format, 1);
        Assert.NotNull(Check(Samples.Resized(previous, previous.Length + 5_000)));
    }

    /// <summary>
    /// Formats that allow data after their end can't be caught here when the previous file was smaller:
    /// it's whole, followed by zeros. CopyEngine catches it (the same data as the file before it, and the read-back).
    /// </summary>
    [Theory]
    [InlineData("JPEG")]
    [InlineData("PNG")]
    public void Previous_whole_file_padded_with_zeros_is_not_caught_here(string format)
    {
        var previous = Samples.Make(format, 1);
        Assert.Null(Check(Samples.Resized(previous, previous.Length + 5_000)));
    }

    [Fact]
    public void A_plist_under_a_photo_name_fails_if_cut() => Assert.NotNull(Check(Samples.Aae(1)[..400]));

    [Fact]
    public void Unknown_format_passes() => Assert.Null(Check(Samples.Random(1, 5_000)));

    [Fact]
    public void Missing_file_fails() => Assert.NotNull(FileCheck.Problem(Path.Combine(dir.Path, "nothing.heic")));
}
