using GsproLighting.Wled.Device;
using Xunit;

namespace GsproLighting.Tests;

public sealed class WledHttpFailureFormatterTests
{
    [Fact]
    public void Format_Empty400_IncludesUrlRequestResponseAndHint()
    {
        var message = WledHttpFailureFormatter.Format(
            "POST",
            new Uri("http://192.168.86.89/json/state"),
            "application/json",
            """{"on":true,"bri":180}""",
            400,
            "Bad Request",
            responseBody: "");

        Assert.Contains("192.168.86.89", message, StringComparison.Ordinal);
        Assert.Contains("/json/state", message, StringComparison.Ordinal);
        Assert.Contains("content-type=application/json", message, StringComparison.Ordinal);
        Assert.Contains("""request={"on":true,"bri":180}""", message, StringComparison.Ordinal);
        Assert.Contains("response=(empty response body)", message, StringComparison.Ordinal);
        Assert.Contains("hint=", message, StringComparison.Ordinal);
        Assert.DoesNotContain("charset=utf-8", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryExtract_PrefixedEffectFailure_PullsIpRequestAndHint()
    {
        var formatted = WledHttpFailureFormatter.Format(
            "POST",
            new Uri("http://192.168.86.89/json/state"),
            "application/json",
            """{"seg":[{"fx":79}]}""",
            400,
            "Bad Request",
            """{"error":9}""");
        var prefixed = $"WLED effect failed: {formatted}";

        Assert.True(WledHttpFailureFormatter.TryExtract(prefixed, out var details));
        Assert.Equal("192.168.86.89", details.Ip);
        Assert.Equal("http://192.168.86.89/json/state", details.Url);
        Assert.Contains("\"fx\":79", details.Request, StringComparison.Ordinal);
        Assert.Contains("\"error\":9", details.Response, StringComparison.Ordinal);
        Assert.Contains("error 9", details.Hint, StringComparison.OrdinalIgnoreCase);
    }
}
