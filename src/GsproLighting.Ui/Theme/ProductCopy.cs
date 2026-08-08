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
        "Preview plays on-screen and on WLED without saving. Colors hold after each animation — Stop holds ready Ripple ambient.";

    public const string WledTabIntro =
        "Live WLED controls from your controller catalogs. Golf reactions still win during shots, then return to Ripple ambient.";

    public const string WledTabTip =
        "Refresh loads effects/palettes/state. Apply writes the editor. Sync ambient restores Ripple + Red Reef. Open full WLED for advanced setup.";

    public const string WledConnectionHelp =
        "Set the controller IP on the Connection tab, then Refresh to load live catalogs.";

    public const string WledQuickActionsHelp =
        "Sync ambient restores Ripple + Red Reef at 15% timing. Open full WLED for Wi‑Fi and advanced editors.";

    public const string WledLookHelp =
        "Pick an effect and palette from the controller lists. Filter to find names quickly.";

    public const string WledMotionHelp =
        "Speed and intensity match WLED sx / ix (0–255). Values show as percent.";

    public const string WledColorsHelp =
        "Primary, secondary, and tertiary colors for the selected segment.";

    public const string WledOptionsHelp =
        "Layered keeps golf flashes over ambient. Extra options 1 / 2 map to WLED's o2 / o3 " +
        "toggles — their meaning depends on the selected effect; check WLED's own effect " +
        "description if unsure.";

    public const string WledBrightnessHelp =
        "Power and brightness are global. Segment chooses which strip range Look / Motion / Colors edits apply to. Press Apply to send.";

    public const string WledPresetsHelp =
        "Apply a saved WLED preset, or advance the active playlist.";

    public const string WledEmptyBeforeRefresh =
        "Nothing loaded yet. Enter the WLED IP on Connection, then press Refresh.";

    public const string WledDrgbNote =
        "Shot flashes use DRGB realtime. After each event the bay returns to WLED effect ambient (HTTP apply with live:false). Wi‑Fi, security, 2D matrix editors, audio config, and firmware stay in the full WLED UI.";

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
