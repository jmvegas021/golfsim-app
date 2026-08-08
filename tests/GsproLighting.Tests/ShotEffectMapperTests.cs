using GsproLighting.Core.Config;
using GsproLighting.Core.Models;
using GsproLighting.Core.Services;
using Xunit;

namespace GsproLighting.Tests;

public sealed class ShotEffectMapperTests
{
    private readonly EffectConfig _effects = new();
    private readonly ShotEffectMapper _mapper = new();

    [Fact]
    public void MapPlan_Putt_SelectsPuttSlotAndDirection()
    {
        var shot = CreateShot(speed: 10, hla: -3, smashFactor: null);

        var plan = _mapper.MapPlan(shot, _effects);

        Assert.True(plan.IsPutt);
        Assert.Same(_effects.Putt, plan.Slot);
        Assert.Equal(ShotDirection.Left, plan.Direction);
    }

    [Fact]
    public void MapPlan_PureStrike_SelectsPureSlotAndDirection()
    {
        var shot = CreateShot(speed: 130, hla: 3, smashFactor: 1.5);

        var plan = _mapper.MapPlan(shot, _effects);

        Assert.False(plan.IsPutt);
        Assert.Same(_effects.PureStrike, plan.Slot);
        Assert.Same(_effects.PureStrike.Color, plan.Color);
        Assert.Equal(ShotDirection.Right, plan.Direction);
    }

    [Fact]
    public void MapPlan_Mishit_SelectsMishitSlotAndDirection()
    {
        var shot = CreateShot(speed: 100, hla: 0.5, smashFactor: 1.1);

        var plan = _mapper.MapPlan(shot, _effects);

        Assert.False(plan.IsPutt);
        Assert.Same(_effects.Mishit, plan.Slot);
        Assert.Equal(ShotDirection.Center, plan.Direction);
    }

    private static ShotPayload CreateShot(
        double speed,
        double hla,
        double? smashFactor) =>
        new()
        {
            BallData = new BallData
            {
                Speed = speed,
                Hla = hla,
                CarryDistance = 150,
                Vla = 15
            },
            MeasuredSmashFactor = smashFactor
        };
}
