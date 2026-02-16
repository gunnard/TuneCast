using TuneCast.Decision.Rules;
using TuneCast.Models;
using Xunit;

namespace TuneCast.Tests.Decision.Rules;

public class BitrateCapRuleTests
{
    private readonly BitrateCapRule _rule = new();

    [Fact]
    public void Mobile_HighBitrate_AppliesCap()
    {
        var result = _rule.Evaluate(Client(ClientType.AndroidMobile), Media(25_000_000));

        Assert.NotNull(result);
        Assert.Equal(8_000_000, result!.BitrateCap);
        Assert.True(result.AllowTranscoding);
    }

    [Fact]
    public void Mobile_LowBitrate_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.AndroidMobile), Media(5_000_000));

        Assert.Null(result);
    }

    [Fact]
    public void SwiftfinIos_HighBitrate_AppliesCap()
    {
        var result = _rule.Evaluate(Client(ClientType.SwiftfinIos), Media(15_000_000));

        Assert.NotNull(result);
        Assert.Equal(8_000_000, result!.BitrateCap);
    }

    [Fact]
    public void Roku_HighBitrate_AppliesCap()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media(40_000_000));

        Assert.NotNull(result);
        Assert.Equal(20_000_000, result!.BitrateCap);
    }

    [Fact]
    public void Roku_WithinCap_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media(15_000_000));

        Assert.Null(result);
    }

    [Fact]
    public void Dlna_HighBitrate_AppliesCap()
    {
        var result = _rule.Evaluate(Client(ClientType.Dlna), Media(30_000_000));

        Assert.NotNull(result);
        Assert.Equal(15_000_000, result!.BitrateCap);
    }

    [Fact]
    public void Desktop_HighBitrate_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Desktop), Media(100_000_000));

        Assert.Null(result);
    }

    [Fact]
    public void AndroidTv_HighBitrate_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.AndroidTv), Media(80_000_000));

        Assert.Null(result);
    }

    [Fact]
    public void NoBitrate_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.AndroidMobile), new MediaModel());

        Assert.Null(result);
    }

    private static ClientModel Client(ClientType type) => new() { ClientType = type, DeviceId = "test" };

    private static MediaModel Media(int bitrate) => new() { Bitrate = bitrate };
}
