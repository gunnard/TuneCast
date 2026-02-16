using System;
using TuneCast.Decision.Rules;
using TuneCast.Models;
using Xunit;

namespace TuneCast.Tests.Decision.Rules;

public class HdrCompatibilityRuleTests
{
    private readonly HdrCompatibilityRule _rule = new();

    // ── SDR content (rule should never fire) ───────────────────────────

    [Fact]
    public void Sdr_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media("SDR"));

        Assert.Null(result);
    }

    [Fact]
    public void EmptyRange_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media(string.Empty));

        Assert.Null(result);
    }

    // ── Web Browser ────────────────────────────────────────────────────

    [Theory]
    [InlineData("HDR10")]
    [InlineData("HLG")]
    public void Web_AnyHdr_RequiresTranscode(string rangeType)
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media(rangeType));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
        Assert.True(result.AllowTranscoding);
        Assert.Equal(RuleSeverity.Require, result.Severity);
    }

    [Fact]
    public void Web_DolbyVision_RequiresTranscode()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media("DoVi"));

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Require, result!.Severity);
        Assert.Contains("Dolby Vision", result.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    // ── Roku ───────────────────────────────────────────────────────────

    [Fact]
    public void Roku_Hdr10_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("HDR10"));

        Assert.Null(result);
    }

    [Fact]
    public void Roku_DolbyVision_Recommends()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("DoVi"));

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Recommend, result!.Severity);
    }

    // ── Mobile ─────────────────────────────────────────────────────────

    [Fact]
    public void Mobile_Hdr_RecommendsTranscode()
    {
        var result = _rule.Evaluate(Client(ClientType.AndroidMobile), Media("HDR10"));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
        Assert.Equal(RuleSeverity.Recommend, result.Severity);
    }

    // ── Desktop / Kodi (HDR passthrough) ───────────────────────────────

    [Fact]
    public void Desktop_Hdr_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Desktop), Media("HDR10"));

        Assert.Null(result);
    }

    [Fact]
    public void Desktop_DolbyVision_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Desktop), Media("DoVi"));

        Assert.Null(result);
    }

    [Fact]
    public void Kodi_Hdr_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Kodi), Media("HLG"));

        Assert.Null(result);
    }

    // ── Apple TV (Swiftfin tvOS) ───────────────────────────────────────

    [Fact]
    public void AppleTv_Hdr10_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.SwiftfinTvos), Media("HDR10"));

        Assert.Null(result);
    }

    [Fact]
    public void AppleTv_DolbyVision_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.SwiftfinTvos), Media("DoVi"));

        Assert.Null(result);
    }

    // ── Xbox ───────────────────────────────────────────────────────────

    [Fact]
    public void Xbox_DolbyVision_Recommends()
    {
        var result = _rule.Evaluate(Client(ClientType.Xbox), Media("DolbyVision"));

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Recommend, result!.Severity);
    }

    [Fact]
    public void Xbox_Hdr10_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Xbox), Media("HDR10"));

        Assert.Null(result);
    }

    private static ClientModel Client(ClientType type) => new() { ClientType = type, DeviceId = "test" };

    private static MediaModel Media(string videoRangeType) => new() { VideoRangeType = videoRangeType };
}
