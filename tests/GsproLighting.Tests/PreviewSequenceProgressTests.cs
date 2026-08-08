using GsproLighting.Core.Preview;
using Xunit;

namespace GsproLighting.Tests;

public sealed class PreviewSequenceProgressTests
{
    [Fact]
    public void FormatLabel_IncludesIndexTotalAndTitle()
    {
        var progress = new PreviewSequenceProgress
        {
            Index = 3,
            Total = 11,
            StateTitle = "Ready / idle"
        };

        Assert.Equal("Play all · 3/11 · Ready / idle", progress.FormatLabel());
    }

    [Fact]
    public void FormatLabel_CompleteUsesHoldingMessage()
    {
        var progress = new PreviewSequenceProgress
        {
            Index = 11,
            Total = 11,
            StateTitle = "done",
            IsComplete = true
        };

        Assert.Equal("Play all complete · last state holding", progress.FormatLabel());
    }
}
