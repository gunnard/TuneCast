using System;
using TuneCast.Decision.Rules;
using TuneCast.Models;
using Xunit;

namespace TuneCast.Tests.Decision.Rules;

public class ContainerCodecCompatibilityRuleTests
{
    private readonly ContainerCodecCompatibilityRule _rule = new();

    // ── Roku ───────────────────────────────────────────────────────────

    [Fact]
    public void Roku_MkvContainer_BlocksDirectPlay()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("h264", "mkv"));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
        Assert.True(result.AllowDirectStream);
        Assert.Equal(RuleSeverity.Require, result.Severity);
    }

    [Fact]
    public void Roku_HevcInMp4_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("hevc", "mp4"));

        Assert.Null(result);
    }

    [Fact]
    public void Roku_HevcInTs_BlocksDirectPlay()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("hevc", "ts"));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
        Assert.Contains("Roku", result.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Roku_H264InMp4_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Roku), Media("h264", "mp4"));

        Assert.Null(result);
    }

    // ── Web Browser ────────────────────────────────────────────────────

    [Theory]
    [InlineData("mkv")]
    [InlineData("avi")]
    [InlineData("wmv")]
    [InlineData("flv")]
    public void Web_UnsupportedContainer_BlocksDirectPlay(string container)
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media("h264", container));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
        Assert.Equal(RuleSeverity.Require, result.Severity);
    }

    [Fact]
    public void Web_Hevc_BlocksDirectPlay()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media("hevc", "mp4"));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
        Assert.True(result.AllowTranscoding);
        Assert.Equal(RuleSeverity.Recommend, result.Severity);
    }

    [Fact]
    public void Web_H264InMp4_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.WebBrowser), Media("h264", "mp4"));

        Assert.Null(result);
    }

    // ── Swiftfin ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("webm")]
    [InlineData("avi")]
    public void Swiftfin_UnsupportedContainer_BlocksDirectPlay(string container)
    {
        var result = _rule.Evaluate(Client(ClientType.SwiftfinIos), Media("h264", container));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
    }

    [Fact]
    public void Swiftfin_Mp4_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.SwiftfinIos), Media("h264", "mp4"));

        Assert.Null(result);
    }

    // ── Xbox ───────────────────────────────────────────────────────────

    [Fact]
    public void Xbox_Mkv_BlocksDirectPlay()
    {
        var result = _rule.Evaluate(Client(ClientType.Xbox), Media("h264", "mkv"));

        Assert.NotNull(result);
        Assert.False(result!.AllowDirectPlay);
    }

    // ── DLNA ───────────────────────────────────────────────────────────

    [Fact]
    public void Dlna_Mkv_BlocksDirectPlay()
    {
        var result = _rule.Evaluate(Client(ClientType.Dlna), Media("h264", "mkv"));

        Assert.NotNull(result);
        Assert.Equal(RuleSeverity.Require, result!.Severity);
    }

    // ── Desktop / Kodi (should never fire) ─────────────────────────────

    [Fact]
    public void Desktop_AnyContainer_NoRule()
    {
        var result = _rule.Evaluate(Client(ClientType.Desktop), Media("hevc", "mkv"));

        Assert.Null(result);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static ClientModel Client(ClientType type) => new() { ClientType = type, DeviceId = "test" };

    private static MediaModel Media(string videoCodec, string container) => new()
    {
        VideoCodec = videoCodec,
        Container = container,
    };
}
