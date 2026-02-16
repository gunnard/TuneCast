using TuneCast.Decision.Rules;
using TuneCast.Models;
using Xunit;

namespace TuneCast.Tests.Decision.Rules;

public class AudioPassthroughRuleTests
{
    private readonly AudioPassthroughRule _rule = new();

    // ── Web Browser ────────────────────────────────────────────────────

    [Theory]
    [InlineData("truehd")]
    [InlineData("dts-hd ma")]
    [InlineData("dts-hd hra")]
    [InlineData("dts")]
    public void Web_LosslessOrDts_RequiresTranscode(string audioCodec)
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media(audioCodec));

        Assert.NotNull(result);
        Assert.True(result!.AllowTranscoding);
        Assert.Equal(RuleSeverity.Require, result.Severity);
    }

    [Fact]
    public void Web_Aac_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media("aac"));

        Assert.Null(result);
    }

    // ── Mobile ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("truehd")]
    [InlineData("dts")]
    [InlineData("dts-hd ma")]
    public void Mobile_LosslessOrDts_RequiresTranscode(string audioCodec)
    {
        var result = _rule.Evaluate(Client(ClientType.AndroidMobile), Media(audioCodec));

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Require, result!.Severity);
    }

    // ── Roku ───────────────────────────────────────────────────────────

    [Fact]
    public void Roku_TrueHD_RequiresTranscode()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("truehd"));

        Assert.NotNull(result);
        Assert.True(result!.AllowTranscoding);
    }

    [Fact]
    public void Roku_Ac3_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("ac3"));

        Assert.Null(result);
    }

    // ── Swiftfin iOS ───────────────────────────────────────────────────

    [Theory]
    [InlineData("dts")]
    [InlineData("dts-hd ma")]
    [InlineData("dts-hd hra")]
    public void SwiftfinIos_Dts_RequiresTranscode(string audioCodec)
    {
        var result = _rule.Evaluate(Client(ClientType.SwiftfinIos), Media(audioCodec));

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Require, result!.Severity);
    }

    [Fact]
    public void SwiftfinIos_Aac_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.SwiftfinIos), Media("aac"));

        Assert.Null(result);
    }

    // ── Desktop / Kodi (passthrough capable) ───────────────────────────

    [Fact]
    public void Desktop_TrueHD_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Desktop), Media("truehd"));

        Assert.Null(result);
    }

    [Fact]
    public void Kodi_DtsHdMa_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Kodi), Media("dts-hd ma"));

        Assert.Null(result);
    }

    // ── Generic client with lossless ───────────────────────────────────

    [Fact]
    public void AndroidTv_TrueHD_Suggests()
    {
        var result = _rule.Evaluate(Client(ClientType.AndroidTv), Media("truehd"));

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Suggest, result!.Severity);
    }

    // ── No audio codec ─────────────────────────────────────────────────

    [Fact]
    public void EmptyAudio_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), new MediaModel());

        Assert.Null(result);
    }

    private static ClientModel Client(ClientType type) => new() { ClientType = type, DeviceId = "test" };

    private static MediaModel Media(string audioCodec) => new() { AudioCodec = audioCodec };
}
