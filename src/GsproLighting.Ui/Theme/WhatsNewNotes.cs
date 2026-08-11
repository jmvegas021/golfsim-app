namespace GsproLighting.Ui.Theme;

/// <summary>Short release notes surfaced in About / What’s New.</summary>
public static class WhatsNewNotes
{
    public const string Headline = "What’s new in this build";

    public static readonly string[] Bullets =
    [
        "Waiting uses native WLED Ripple over HTTP (live:false) — Preview Waiting matches live OnWaitingAsync and honors StatusTuning speed/intensity/layers.",
        "Fix stuck Waiting: Connect-loading edges no longer leave the bay on aqua after Ready/Not Ready resumes.",
        "Garmin R50 sparse Force metrics: radians→degrees and better shot assembly from incomplete Connect log lines.",
        "Status effect tuning on Connection (Ready / Not Ready / Waiting / Direction sx·ix·layers / band size) with night-theme checkboxes.",
        "After a shot, Direction holds 4s then falls back to Not Ready DDP if GSPro never sends Not Ready — real Ready/Not Ready still win.",
        "Legend and Quick Control mark Pure / Mishit / Putt / Celebrate / Hazard as Preview-only; live shots drive Direction only.",
    ];
}
