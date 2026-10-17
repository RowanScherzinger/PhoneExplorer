using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhoneExplorer;

/// <summary>
/// Updates the app from the GitHub releases of RowanScherzinger/PhoneExplorer. Windows lets a running
/// exe be renamed but not overwritten, so an update renames this exe to .old, moves the download into
/// its place and starts it; the new copy deletes the .old file when it starts.
/// </summary>
public static class Updater
{
    public const string ReleasesPage = "https://github.com/RowanScherzinger/PhoneExplorer/releases";
    const string LatestUrl = "https://api.github.com/repos/RowanScherzinger/PhoneExplorer/releases/latest";
    const string AssetName = "PhoneExplorer.exe";

    /// <summary>A newer release. Sha256 is null when GitHub doesn't report a digest for the file.</summary>
    public sealed record Release(Version Version, string Url, long Size, string? Sha256);

    public static Version Current { get; } = Normalise(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    static Version Normalise(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    static HttpClient Client()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        // GitHub's API refuses requests without a User-Agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"PhoneExplorer/{Current}");
        return http;
    }

    /// <summary>The latest release if it is newer than this copy and has the exe attached; null otherwise, or when offline.</summary>
    public static async Task<Release?> CheckAsync()
    {
        try
        {
            using var http = Client();
            var latest = await http.GetFromJsonAsync<GitHubRelease>(LatestUrl);
            if (latest == null || latest.Draft || latest.Prerelease) return null;
            if (!Version.TryParse(latest.TagName.TrimStart('v', 'V'), out var version)) return null;
            version = Normalise(version);
            if (version <= Current) return null;
            var asset = latest.Assets.FirstOrDefault(a => a.Name.Equals(AssetName, StringComparison.OrdinalIgnoreCase));
            if (asset == null) return null;
            var sha = asset.Digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? asset.Digest[7..] : null;
            return new Release(version, asset.Url, asset.Size, sha);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException) { return null; }
    }

    /// <summary>
    /// Downloads the release next to this exe, checks it, swaps it in and starts it.
    /// Returns null when the new copy is running and this one should quit, or an error message.
    /// </summary>
    public static async Task<string?> InstallAsync(Release release, IProgress<(long Done, long Total)> progress)
    {
        var exe = Environment.ProcessPath;
        if (exe == null) return "Couldn't find where the app is running from.";
        string fresh = exe + ".new", old = exe + ".old";
        try
        {
            using (var http = Client())
            using (var response = await http.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = File.Create(fresh);
                var buffer = new byte[81920];
                long done = 0;
                int n;
                while ((n = await source.ReadAsync(buffer)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, n));
                    done += n;
                    progress.Report((done, release.Size));
                }
            }
            if (new FileInfo(fresh).Length != release.Size) throw new IOException("The download was incomplete.");
            if (release.Sha256 != null)
            {
                await using var file = File.OpenRead(fresh);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(file));
                if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("The download was damaged.");
            }

            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try { File.Move(fresh, exe); }
            catch { File.Move(old, exe); throw; }
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            try { File.Delete(fresh); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            return ex.Message.Trim();
        }
    }

    /// <summary>Deletes the files an update leaves behind. The old copy may still be closing, so keep trying for a few seconds.</summary>
    public static void CleanUp()
    {
        var exe = Environment.ProcessPath;
        if (exe == null) return;
        Task.Run(async () =>
        {
            foreach (var leftover in new[] { exe + ".old", exe + ".new" })
                for (int i = 0; i < 20 && File.Exists(leftover); i++)
                {
                    try { File.Delete(leftover); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { await Task.Delay(500); }
                }
        });
    }

    sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = new();
    }

    sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("browser_download_url")] public string Url { get; set; } = "";
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}
