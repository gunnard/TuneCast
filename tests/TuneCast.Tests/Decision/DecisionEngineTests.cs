using System;
using System.Collections.Generic;
using TuneCast.Configuration;
using TuneCast.Decision;
using TuneCast.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace TuneCast.Tests.Decision;

/// <summary>
/// Decision engine tests — pure logic, deterministic, no Jellyfin runtime needed.
/// </summary>
public class DecisionEngineTests
{
    private readonly DecisionEngineService _engine;

    public DecisionEngineTests()
    {
        var logger = new Mock<ILogger<DecisionEngineService>>();
        var config = new PluginConfiguration
        {
            ConservativeMode = false,
            EnableDynamicProfiles = true,
        };
        _engine = new DecisionEngineService(logger.Object, () => config);
    }

    [Fact]
    public void HighConfidenceCodec_FavorsDirectPlay()
    {
        var client = CreateClient(ClientType.Desktop, codecConfidence: new() { ["h264"] = 0.95 });
        var media = CreateMedia(videoCodec: "h264", container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        Assert.True(policy.AllowDirectPlay);
    }

    [Fact]
    public void LowConfidenceCodec_DisablesDirectPlay()
    {
        var client = CreateClient(ClientType.WebBrowser, codecConfidence: new() { ["hevc"] = 0.2 });
        var media = CreateMedia(videoCodec: "hevc", container: "mkv");

        var policy = _engine.ComputePolicy(client, media);

        Assert.False(policy.AllowDirectPlay);
        Assert.True(policy.AllowTranscoding);
    }

    [Fact]
    public void MediumConfidenceCodec_AllowsDirectPlayWithFallback()
    {
        var client = CreateClient(ClientType.AndroidTv, codecConfidence: new() { ["hevc"] = 0.55 });
        var media = CreateMedia(videoCodec: "hevc", container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        Assert.True(policy.AllowDirectPlay);
        Assert.True(policy.AllowDirectStream);
    }

    [Fact]
    public void BitrateExceedsCap_SetsBitrateCap()
    {
        var client = CreateClient(ClientType.Roku, maxBitrate: 8_000_000, codecConfidence: new() { ["h264"] = 0.9 });
        var media = CreateMedia(videoCodec: "h264", container: "mp4", bitrate: 25_000_000);

        var policy = _engine.ComputePolicy(client, media);

        Assert.NotNull(policy.BitrateCap);
        Assert.Equal(8_000_000, policy.BitrateCap);
        Assert.True(policy.AllowTranscoding);
    }

    [Fact]
    public void BitrateWithinCap_NoBitrateOverride()
    {
        var client = CreateClient(ClientType.Desktop, maxBitrate: 50_000_000, codecConfidence: new() { ["h264"] = 0.95 });
        var media = CreateMedia(videoCodec: "h264", container: "mp4", bitrate: 10_000_000);

        var policy = _engine.ComputePolicy(client, media);

        Assert.Null(policy.BitrateCap);
    }

    [Fact]
    public void UnknownCodec_NoEntry_DefersToJellyfin()
    {
        var client = CreateClient(ClientType.Unknown);
        var media = CreateMedia(videoCodec: "vvc", container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        // No confidence data for codec → engine defers, allows all methods
        Assert.True(policy.AllowDirectPlay);
        Assert.Contains("no confidence data", policy.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownCodec_ExplicitlyLow_DisablesDirectPlay()
    {
        var client = CreateClient(ClientType.Unknown, codecConfidence: new() { ["vvc"] = 0.1 });
        var media = CreateMedia(videoCodec: "vvc", container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        // Explicitly low confidence → direct play disabled
        Assert.False(policy.AllowDirectPlay);
        Assert.True(policy.AllowTranscoding);
    }

    [Fact]
    public void EmptyMediaCodec_AllowsAllMethods()
    {
        var client = CreateClient(ClientType.Desktop, codecConfidence: new() { ["h264"] = 0.95 });
        var media = CreateMedia(videoCodec: string.Empty, container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        Assert.True(policy.AllowDirectPlay);
        Assert.True(policy.AllowDirectStream);
        Assert.True(policy.AllowTranscoding);
    }

    [Fact]
    public void PolicyReasoning_IsPopulated()
    {
        var client = CreateClient(ClientType.WebBrowser, codecConfidence: new() { ["hevc"] = 0.15 });
        var media = CreateMedia(videoCodec: "hevc", container: "mkv", width: 3840, height: 2160);

        var policy = _engine.ComputePolicy(client, media);

        Assert.False(string.IsNullOrWhiteSpace(policy.Reasoning));
        Assert.Contains("hevc", policy.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtremeTranscodeCost_WarnsInReasoning()
    {
        var client = CreateClient(ClientType.WebBrowser, codecConfidence: new() { ["hevc"] = 0.1 });
        var media = CreateMedia(
            videoCodec: "hevc",
            container: "mkv",
            width: 3840,
            height: 2160,
            bitDepth: 10,
            videoRangeType: "HDR10",
            hasImageSubs: true);

        var policy = _engine.ComputePolicy(client, media);

        Assert.Contains("EXTREME", policy.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    // ── Audio codec evaluation ─────────────────────────────────────────

    [Fact]
    public void HighConfidenceAudio_MentionsCompatible()
    {
        var client = CreateClient(ClientType.Desktop, codecConfidence: new()
        {
            ["h264"] = 0.95,
            ["aac"] = 0.95,
        });
        var media = CreateMedia(videoCodec: "h264", audioCodec: "aac", container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        Assert.Contains("aac", policy.Reasoning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compatible", policy.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LowConfidenceAudio_EnablesTranscode()
    {
        var client = CreateClient(ClientType.WebBrowser, codecConfidence: new()
        {
            ["h264"] = 0.95,
            ["truehd"] = 0.0,
        });
        var media = CreateMedia(videoCodec: "h264", audioCodec: "truehd", container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        Assert.True(policy.AllowTranscoding);
        Assert.Contains("audio transcode", policy.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AudioOnlyMismatch_PrefersDirectStreamOverFullTranscode()
    {
        var client = CreateClient(ClientType.WebBrowser, codecConfidence: new()
        {
            ["h264"] = 0.95,
            ["dts"] = 0.0,
        }, containerConfidence: new()
        {
            ["mp4"] = 0.95,
        });
        var media = CreateMedia(videoCodec: "h264", audioCodec: "dts", container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        Assert.True(policy.AllowDirectPlay);
        Assert.True(policy.AllowDirectStream);
        Assert.True(policy.AllowTranscoding);
    }

    // ── Container confidence ───────────────────────────────────────────

    [Fact]
    public void LowContainerConfidence_ForcesRemuxOverDirectPlay()
    {
        var client = CreateClient(ClientType.WebBrowser, codecConfidence: new()
        {
            ["h264"] = 0.95,
        }, containerConfidence: new()
        {
            ["mkv"] = 0.1,
        });
        var media = CreateMedia(videoCodec: "h264", container: "mkv");

        var policy = _engine.ComputePolicy(client, media);

        Assert.False(policy.AllowDirectPlay);
        Assert.True(policy.AllowDirectStream);
    }

    [Fact]
    public void HighContainerConfidence_BoostsConfidence()
    {
        var client = CreateClient(ClientType.Desktop, codecConfidence: new()
        {
            ["h264"] = 0.95,
        }, containerConfidence: new()
        {
            ["mp4"] = 0.95,
        });
        var media = CreateMedia(videoCodec: "h264", container: "mp4");

        var policy = _engine.ComputePolicy(client, media);

        Assert.True(policy.AllowDirectPlay);
        Assert.True(policy.Confidence >= 0.7);
    }

    // ── Full spectrum combos ───────────────────────────────────────────

    [Fact]
    public void RokuHevcInMkv_DisablesDirectPlay()
    {
        var client = CreateClient(ClientType.Roku, codecConfidence: new()
        {
            ["hevc"] = 0.6,
        }, containerConfidence: new()
        {
            ["mkv"] = 0.1,
        });
        var media = CreateMedia(videoCodec: "hevc", container: "mkv");

        var policy = _engine.ComputePolicy(client, media);

        Assert.False(policy.AllowDirectPlay);
        Assert.True(policy.AllowDirectStream);
    }

    [Fact]
    public void DesktopAV1InWebm_FavorsDirectPlay()
    {
        var client = CreateClient(ClientType.Desktop, codecConfidence: new()
        {
            ["av1"] = 0.7,
        }, containerConfidence: new()
        {
            ["webm"] = 0.95,
        });
        var media = CreateMedia(videoCodec: "av1", container: "webm");

        var policy = _engine.ComputePolicy(client, media);

        Assert.True(policy.AllowDirectPlay);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static ClientModel CreateClient(
        ClientType clientType,
        Dictionary<string, double>? codecConfidence = null,
        Dictionary<string, double>? containerConfidence = null,
        int? maxBitrate = null)
    {
        return new ClientModel
        {
            DeviceId = $"test-device-{Guid.NewGuid():N}",
            ClientType = clientType,
            ClientName = clientType.ToString(),
            CodecConfidence = codecConfidence ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            ContainerConfidence = containerConfidence ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
            MaxBitrate = maxBitrate,
            ReliabilityScore = 0.5,
        };
    }

    private static MediaModel CreateMedia(
        string videoCodec = "h264",
        string audioCodec = "aac",
        string container = "mp4",
        int? bitrate = null,
        int? width = 1920,
        int? height = 1080,
        int? bitDepth = 8,
        string videoRangeType = "SDR",
        int audioChannels = 2,
        bool hasImageSubs = false,
        bool hasTextSubs = false)
    {
        var model = new MediaModel
        {
            MediaSourceId = $"test-source-{Guid.NewGuid():N}",
            ItemId = $"test-item-{Guid.NewGuid():N}",
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            Container = container,
            Bitrate = bitrate,
            Width = width,
            Height = height,
            VideoBitDepth = bitDepth,
            VideoRangeType = videoRangeType,
            AudioChannels = audioChannels,
            HasImageSubtitles = hasImageSubs,
            HasTextSubtitles = hasTextSubs,
        };

        model.TranscodeCostEstimate = TuneCast.Intelligence.MediaIntelligenceService.EstimateTranscodeCost(model);
        return model;
    }
}
