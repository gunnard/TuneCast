using TuneCast.Decision.Rules;
using TuneCast.Models;
using Xunit;

namespace TuneCast.Tests.Decision.Rules;

public class BitDepthCompatibilityRuleTests
{
    private readonly BitDepthCompatibilityRule _rule = new();

    [Fact]
    public void H264_10bit_AlwaysBlocksDirectPlay()
    {
        var result = _rule.Evaluate(Client(ClientType.Desktop), Media("h264", 10));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
        Assert.True(result.AllowTranscoding);
        Assert.Equal(RuleSeverity.Require, result.Severity);
        Assert.Contains("Hi10P", result.Reasoning);
    }

    [Fact]
    public void H264_8bit_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media("h264", 8));

        Assert.Null(result);
    }

    [Fact]
    public void Hevc_10bit_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media("hevc", 10));

        Assert.Null(result);
    }

    [Fact]
    public void Any_12bit_OnNonDesktop_BlocksDirectPlay()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("hevc", 12));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
        Assert.Equal(RuleSeverity.Recommend, result.Severity);
    }

    [Fact]
    public void Desktop_12bit_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Desktop), Media("hevc", 12));

        Assert.Null(result);
    }

    [Fact]
    public void Kodi_12bit_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Kodi), Media("hevc", 12));

        Assert.Null(result);
    }

    [Fact]
    public void NoBitDepth_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), new MediaModel { VideoCodec = "h264" });

        Assert.Null(result);
    }

    private static ClientModel Client(ClientType type) => new() { ClientType = type, DeviceId = "test" };

    private static MediaModel Media(string codec, int bitDepth) => new()
    {
        VideoCodec = codec,
        VideoBitDepth = bitDepth,
    };
}
