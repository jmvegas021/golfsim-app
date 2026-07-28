using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using GsproLighting.Core.Config;

namespace GsproLighting.Ui.Updates;

/// <summary>
/// Fallback updater for non-Velopack (zip/portable) installs via GitHub Releases.
/// </summary>
public sealed class PortableGitHubUpdater
{
    public const string ZipAssetName = "GsproLighting-windows-x64.zip";

    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    public PortableGitHubUpdater()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("GsproLighting", AppVersionInfo.Current));
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<(string Tag, string DownloadUrl)?> FindNewerReleaseAsync(
        string currentVersion,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(
            "https://api.github.com/repos/jmvegas021/golfsim-app/releases/latest",
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!AppVersionInfo.TryParseSemVer(tag, out var remote) ||
            !AppVersionInfo.TryParseSemVer(currentVersion, out var local) ||
            remote <= local)
            return null;

        string? url = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (!string.Equals(name, ZipAssetName, StringComparison.OrdinalIgnoreCase))
                    continue;
                url = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(
                $"Release {tag} has no {ZipAssetName} asset.");

        return (tag.TrimStart('v', 'V'), url);
    }

    public async Task<string> DownloadZipAsync(
        string downloadUrl,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var target = Path.Combine(
            Path.GetTempPath(),
            $"GsproLighting-update-{Guid.NewGuid():N}.zip");

        using var response = await _http.GetAsync(
            downloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var output = File.Create(target);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
            readTotal += read;
            if (total > 0)
                progress?.Report(readTotal / (double)total);
        }

        return target;
    }

    public void ApplyZipAndRestart(string zipPath)
    {
        var installDir = AppPaths.InstallDirectory;
        var staging = Path.Combine(
            Path.GetTempPath(),
            $"GsproLighting-stage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        ZipFile.ExtractToDirectory(zipPath, staging, overwriteFiles: true);

        var exeName = Path.GetFileName(Environment.ProcessPath) ?? "GsproLighting.exe";
        var stagedExe = Path.Combine(staging, exeName);
        if (!File.Exists(stagedExe))
        {
            var fallback = Directory.GetFiles(staging, "GsproLighting.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (fallback is null)
                throw new InvalidOperationException("Update zip did not contain GsproLighting.exe.");
            staging = Path.GetDirectoryName(fallback)!;
            stagedExe = fallback;
        }

        var pid = Environment.ProcessId;
        var batPath = Path.Combine(Path.GetTempPath(), $"GsproLighting-apply-{pid}.cmd");
        var bat = $"""
            @echo off
            setlocal
            :wait
            tasklist /FI "PID eq {pid}" | find "{pid}" >nul
            if not errorlevel 1 (
              timeout /t 1 /nobreak >nul
              goto wait
            )
            xcopy /E /Y /I /Q "{staging}\*" "{installDir}\"
            start "" "{Path.Combine(installDir, exeName)}"
            rmdir /S /Q "{staging}"
            del /F /Q "{zipPath}" >nul 2>&1
            del /F /Q "%~f0"
            """;
        File.WriteAllText(batPath, bat);

        Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        });
    }
}
