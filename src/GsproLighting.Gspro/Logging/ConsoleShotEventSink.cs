using GsproLighting.Core.Contracts;
using GsproLighting.Core.Models;

namespace GsproLighting.Gspro.Logging;

/// <summary>
/// Console sink used by spike / v0.1 to print parsed shots and flag unknown fields.
/// </summary>
public sealed class ConsoleShotEventSink : IShotEventSink
{
    public Task OnShotAsync(ShotPayload shot, CancellationToken cancellationToken = default)
    {
        var ball = shot.BallData;
        var smash = shot.SmashFactor is double s ? $" smash={s:F2}" : " smash=n/a";
        Console.WriteLine(
            $"[SHOT #{shot.ShotNumber}] speed={ball?.Speed:F1} hla={ball?.Hla:F1} " +
            $"vla={ball?.Vla:F1} spinAxis={ball?.SpinAxis:F1} totalSpin={ball?.TotalSpin:F0} " +
            $"carry={ball?.CarryDistance:F1}{smash} club={shot.ClubData?.Speed:F1}");
        return Task.CompletedTask;
    }

    public Task OnPlayerInfoAsync(GsproResponse response, CancellationToken cancellationToken = default)
    {
        var label = response.Code switch
        {
            200 => "ACK",
            201 => "PLAYER",
            >= 500 and < 600 => "ERROR",
            _ => "RESPONSE"
        };

        Console.WriteLine(
            $"[{label} code={response.Code}] handed={response.Player?.Handed} club={response.Player?.Club} " +
            $"msg={response.Message}");

        if (response.Extensions.Count > 0)
            Console.WriteLine($"  ! undocumented fields: {string.Join(", ", response.Extensions.Keys)}");

        return Task.CompletedTask;
    }

    public Task OnBallReadyAsync(ShotPayload payload, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[BALL READY] LaunchMonitorBallDetected");
        return Task.CompletedTask;
    }
}
