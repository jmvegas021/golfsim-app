using Velopack;
using Velopack.Sources;

namespace GsproLighting.Ui.Updates;

/// <summary>
/// Velopack (preferred) + portable GitHub Releases zip fallback.
/// </summary>
public sealed class AppUpdateService
{
    public const string RepoUrl = "https://github.com/jmvegas021/golfsim-app";

    private readonly object _gate = new();
    private readonly PortableGitHubUpdater _portable = new();
    private readonly UpdateManager? _velopack;
    private UpdateInfo? _velopackUpdate;
    private string? _portableZipPath;
    private string? _availableVersion;
    private UpdatePhase _phase = UpdatePhase.Idle;
    private string _status = "Not checked yet.";
    private int _busy;

    public AppUpdateService()
    {
        CurrentVersion = AppVersionInfo.Current;
        try
        {
            _velopack = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
        }
        catch
        {
            _velopack = null;
        }

        if (_velopack?.IsInstalled == true)
            _status = $"Installed via Setup · v{CurrentVersion}";
        else
            _status = $"Portable / zip install · v{CurrentVersion} (zip updates supported)";
    }

    public string CurrentVersion { get; }
    public bool IsVelopackInstall => _velopack?.IsInstalled == true;
    public event Action? Changed;

    public AppUpdateSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new AppUpdateSnapshot
            {
                CurrentVersion = CurrentVersion,
                Phase = _phase,
                StatusText = _status,
                AvailableVersion = _availableVersion,
                CanInstall = _phase == UpdatePhase.ReadyToInstall,
                IsVelopackInstall = IsVelopackInstall
            };
        }
    }

    /// <summary>Launch-time check: detect only (no download) so play is not interrupted.</summary>
    public async Task CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
            return;

        try
        {
            Set(UpdatePhase.Checking, "Checking GitHub Releases…");

            if (_velopack?.IsInstalled == true)
            {
                var info = await _velopack.CheckForUpdatesAsync().ConfigureAwait(false);
                if (info is null)
                {
                    Set(UpdatePhase.UpToDate, $"Up to date (v{CurrentVersion}).");
                    return;
                }

                _velopackUpdate = info;
                _availableVersion = info.TargetFullRelease.Version.ToString();
                Set(
                    UpdatePhase.Available,
                    $"Update available: v{_availableVersion}. Open Settings → Check for updates to download.");
                return;
            }

            var newer = await _portable.FindNewerReleaseAsync(CurrentVersion, cancellationToken)
                .ConfigureAwait(false);
            if (newer is null)
            {
                Set(UpdatePhase.UpToDate, $"Up to date (v{CurrentVersion}).");
                return;
            }

            _availableVersion = newer.Value.Tag;
            Set(
                UpdatePhase.Available,
                $"Update available: v{_availableVersion}. Open Settings → Check for updates to download.");
        }
        catch (Exception ex)
        {
            Set(UpdatePhase.Error, $"Update check failed: {FormatUpdateError(ex)}");
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    /// <summary>Settings / tray: check, download, then enable Install.</summary>
    public async Task CheckAndDownloadAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _busy, 1) == 1)
            return;

        try
        {
            Set(UpdatePhase.Checking, "Checking GitHub Releases…");

            if (_velopack?.IsInstalled == true)
            {
                await CheckVelopackAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            await CheckPortableAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Set(UpdatePhase.Error, $"Update failed: {FormatUpdateError(ex)}");
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    public void ApplyInstallAndRestart()
    {
        var snap = Snapshot();
        if (!snap.CanInstall)
            throw new InvalidOperationException("No update is ready to install.");

        if (_velopack?.IsInstalled == true)
        {
            if (_velopackUpdate is null)
                throw new InvalidOperationException("Velopack update payload missing.");
            _velopack.ApplyUpdatesAndRestart(_velopackUpdate);
            return;
        }

        if (string.IsNullOrWhiteSpace(_portableZipPath) || !File.Exists(_portableZipPath))
            throw new InvalidOperationException("Downloaded update zip missing.");

        _portable.ApplyZipAndRestart(_portableZipPath);
        Application.Exit();
    }

    private async Task CheckVelopackAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = await _velopack!.CheckForUpdatesAsync().ConfigureAwait(false);
        if (info is null)
        {
            Set(UpdatePhase.UpToDate, $"Up to date (v{CurrentVersion}).");
            return;
        }

        _velopackUpdate = info;
        _availableVersion = info.TargetFullRelease.Version.ToString();
        Set(UpdatePhase.Downloading, $"Downloading v{_availableVersion}…");
        await _velopack.DownloadUpdatesAsync(info).ConfigureAwait(false);
        Set(
            UpdatePhase.ReadyToInstall,
            $"Update v{_availableVersion} ready. Click Install update & restart.");
    }

    private async Task CheckPortableAsync(CancellationToken cancellationToken)
    {
        var newer = await _portable.FindNewerReleaseAsync(CurrentVersion, cancellationToken)
            .ConfigureAwait(false);
        if (newer is null)
        {
            Set(UpdatePhase.UpToDate, $"Up to date (v{CurrentVersion}).");
            return;
        }

        _availableVersion = newer.Value.Tag;
        Set(UpdatePhase.Available, $"Update available: v{_availableVersion}. Downloading…");
        Set(UpdatePhase.Downloading, $"Downloading v{_availableVersion}…");

        var progress = new Progress<double>(p =>
            Set(UpdatePhase.Downloading, $"Downloading v{_availableVersion}… {(int)(p * 100)}%"));

        _portableZipPath = await _portable.DownloadZipAsync(
            newer.Value.DownloadUrl,
            progress,
            cancellationToken).ConfigureAwait(false);

        Set(
            UpdatePhase.ReadyToInstall,
            $"Update v{_availableVersion} ready. Click Install update & restart.");
    }

    private void Set(UpdatePhase phase, string status)
    {
        lock (_gate)
        {
            _phase = phase;
            _status = status;
        }

        Changed?.Invoke();
    }

    private static string FormatUpdateError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("does not indicate success", StringComparison.OrdinalIgnoreCase))
        {
            return $"{message} (feed: {RepoUrl}/releases — needs public releases.win.json)";
        }

        return message;
    }
}
