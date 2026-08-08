namespace GsproLighting.Ui.Theme;

/// <summary>First-run and empty-state microcopy for night-bay chrome.</summary>
public static class ProductCopy
{
    public const string BrandSubtitle =
        "Night-bay WLED control · shot-reactive strip for GSPro";

    public const string ConnectionIntroTitle = "Connection";
    public const string ConnectionIntroBody =
        "Point the strip at your WLED controller, set Open Connect ports if you use a proxy, then tune shot thresholds.";

    public const string NoWledTitle = "No WLED controller yet";
    public const string NoWledBody =
        "Enter your controller IP below, then open Effects → Test lights. The strip stays dark until an address is set.";

    public const string WaitingR50Title = "Waiting for R50 / Connect";
    public const string WaitingR50Body =
        "Start GSPro Connect with your Garmin R50. Auto-watch is on by default — feed lines appear when ready-state and shots land.";

    public const string LiveFeedWaitingTitle = "Live feed waiting";
    public const string LiveFeedWaitingBody =
        "Ready, shot, putt, and player events show here once Connect is live. Clear, open the logs folder, or export a zip for support.";

    public const string PreviewHint =
        "Preview plays on-screen and on WLED without saving. Colors hold after each animation — Stop holds ready green.";

    public const string UpdatesIntro =
        "Check GitHub Releases for Setup or portable zip updates. Install only when you are between rounds.";

    public const string TrayRunning =
        "Still running in the tray. Right-click the icon for settings, lights, and exit.";

    public const string TrayMinimized =
        "Running in the tray. Double-click the icon to open settings.";

    public const string SupportRepoLabel = "Support & releases";

    public const string LicenseSummary =
        "Proprietary software. All rights reserved. Published binaries may be used for personal or commercial bay lighting; redistribution or modification requires permission.";

    public const string LicenseLinkLabel = "View LICENSE";

    public const string LicenseUrl =
        "https://github.com/jmvegas021/golfsim-app/blob/main/LICENSE";
}
