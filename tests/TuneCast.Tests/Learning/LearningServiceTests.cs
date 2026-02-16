using System;
using System.Collections.Generic;
using System.Linq;
using TuneCast.Configuration;
using TuneCast.Learning;
using TuneCast.Models;
using TuneCast.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace TuneCast.Tests.Learning;

/// <summary>
/// Tests for the learning system — verifies confidence adjustments
/// from playback outcomes are correct, bounded, and persistent.
/// </summary>
public class LearningServiceTests
{
    private readonly Mock<IPluginDataStore> _dataStore;
    private readonly LearningService _service;

    public LearningServiceTests()
    {
        _dataStore = new Mock<IPluginDataStore>();
        var logger = new Mock<ILogger<LearningService>>();
        var config = new PluginConfiguration { EnableLearning = true };
        _service = new LearningService(_dataStore.Object, logger.Object, () => config);
    }

    // ── Outcome Classification ─────────────────────────────────────────

    [Fact]
    public void ClassifyOutcome_DirectPlay_LongSession_Success()
    {
        var outcome = Outcome("DirectPlay", playedTicks: 5000, totalTicks: 10000);

        LearningService.ClassifyOutcome(outcome);

        Assert.Equal(PlaybackResult.Success, outcome.Result);
    }

    [Fact]
    public void ClassifyOutcome_VeryShortSession_SuspectedFailure()
    {
        var outcome = Outcome("DirectPlay", playedTicks: 100, totalTicks: 100000);

        LearningService.ClassifyOutcome(outcome);

        Assert.Equal(PlaybackResult.SuspectedFailure, outcome.Result);
    }

    [Fact]
    public void ClassifyOutcome_Transcode_MarkedAsTranscoded()
    {
        var outcome = Outcome("Transcode", playedTicks: 5000, totalTicks: 10000);

        LearningService.ClassifyOutcome(outcome);

        Assert.Equal(PlaybackResult.Transcoded, outcome.Result);
    }

    [Fact]
    public void ClassifyOutcome_NoTicks_RemainsUnknown()
    {
        var outcome = Outcome("DirectPlay", playedTicks: null, totalTicks: null);

        LearningService.ClassifyOutcome(outcome);

        Assert.Equal(PlaybackResult.Unknown, outcome.Result);
    }

    [Fact]
    public void ClassifyOutcome_AlreadyClassified_DoesNotChange()
    {
        var outcome = Outcome("DirectPlay", playedTicks: 100, totalTicks: 100000);
        outcome.Result = PlaybackResult.Failure;

        LearningService.ClassifyOutcome(outcome);

        Assert.Equal(PlaybackResult.Failure, outcome.Result);
    }

    // ── Playback Ratio ─────────────────────────────────────────────────

    [Fact]
    public void ComputePlaybackRatio_ValidTicks_ReturnsRatio()
    {
        var outcome = Outcome("DirectPlay", playedTicks: 3000, totalTicks: 10000);

        double ratio = LearningService.ComputePlaybackRatio(outcome);

        Assert.Equal(0.3, ratio, 2);
    }

    [Fact]
    public void ComputePlaybackRatio_NullTicks_ReturnsNegative()
    {
        var outcome = Outcome("DirectPlay", playedTicks: null, totalTicks: null);

        double ratio = LearningService.ComputePlaybackRatio(outcome);

        Assert.True(ratio < 0);
    }

    [Fact]
    public void ComputePlaybackRatio_ZeroTotal_ReturnsNegative()
    {
        var outcome = Outcome("DirectPlay", playedTicks: 100, totalTicks: 0);

        double ratio = LearningService.ComputePlaybackRatio(outcome);

        Assert.True(ratio < 0);
    }

    // ── Confidence Adjustments (ProcessOutcome) ────────────────────────

    [Fact]
    public void DirectPlaySuccess_IncreasesVideoCodecConfidence()
    {
        var client = Client();
        client.CodecConfidence["hevc"] = 0.5;
        var outcome = SuccessOutcome("DirectPlay", "hevc", "aac", "mkv");

        _service.ProcessOutcome(outcome, client);

        Assert.True(client.CodecConfidence["hevc"] > 0.5);
        _dataStore.Verify(d => d.UpsertClientModel(client), Times.Once);
    }

    [Fact]
    public void TranscodeOutcome_DecreasesVideoCodecConfidence()
    {
        var client = Client();
        client.CodecConfidence["hevc"] = 0.5;
        var outcome = SuccessOutcome("Transcode", "hevc", "aac", "mkv");
        outcome.Result = PlaybackResult.Transcoded;

        _service.ProcessOutcome(outcome, client);

        Assert.True(client.CodecConfidence["hevc"] < 0.5);
    }

    [Fact]
    public void SuspectedFailure_DecreasesConfidence()
    {
        var client = Client();
        client.CodecConfidence["h264"] = 0.8;
        var outcome = Outcome("DirectPlay", playedTicks: 50, totalTicks: 100000);
        outcome.VideoCodec = "h264";
        outcome.AudioCodec = "aac";
        outcome.Container = "mp4";
        outcome.Result = PlaybackResult.SuspectedFailure;

        _service.ProcessOutcome(outcome, client);

        Assert.True(client.CodecConfidence["h264"] < 0.8);
    }

    [Fact]
    public void DirectPlaySuccess_IncreasesContainerConfidence()
    {
        var client = Client();
        client.ContainerConfidence["mkv"] = 0.5;
        var outcome = SuccessOutcome("DirectPlay", "hevc", "aac", "mkv");

        _service.ProcessOutcome(outcome, client);

        Assert.True(client.ContainerConfidence["mkv"] > 0.5);
    }

    [Fact]
    public void EmptyCodec_NoAdjustment()
    {
        var client = Client();
        var outcome = SuccessOutcome("DirectPlay", string.Empty, string.Empty, string.Empty);

        _service.ProcessOutcome(outcome, client);

        Assert.Empty(client.CodecConfidence);
        Assert.Empty(client.ContainerConfidence);
    }

    [Fact]
    public void Confidence_NeverExceedsMax()
    {
        var client = Client();
        client.CodecConfidence["h264"] = 0.99;
        var outcome = SuccessOutcome("DirectPlay", "h264", "aac", "mp4");

        for (int i = 0; i < 100; i++)
        {
            _service.ProcessOutcome(outcome, client);
        }

        Assert.True(client.CodecConfidence["h264"] <= 1.0);
    }

    [Fact]
    public void Confidence_NeverGoesBelowMin()
    {
        var client = Client();
        client.CodecConfidence["hevc"] = 0.01;
        var outcome = SuccessOutcome("Transcode", "hevc", "aac", "mkv");
        outcome.Result = PlaybackResult.Failure;

        for (int i = 0; i < 100; i++)
        {
            _service.ProcessOutcome(outcome, client);
        }

        Assert.True(client.CodecConfidence["hevc"] >= 0.0);
    }

    [Fact]
    public void NewCodec_CreatesEntry()
    {
        var client = Client();
        Assert.False(client.CodecConfidence.ContainsKey("av1"));

        var outcome = SuccessOutcome("DirectPlay", "av1", "opus", "webm");

        _service.ProcessOutcome(outcome, client);

        Assert.True(client.CodecConfidence.ContainsKey("av1"));
        Assert.True(client.CodecConfidence["av1"] > 0.0);
    }

    // ── Recalibration ──────────────────────────────────────────────────

    [Fact]
    public void RecalibrateClient_WithEnoughData_SetsConfidence()
    {
        var client = Client();
        client.CodecConfidence["hevc"] = 0.2;

        var outcomes = Enumerable.Range(0, 10).Select(i =>
        {
            var o = SuccessOutcome("DirectPlay", "hevc", "aac", "mkv");
            o.DeviceId = client.DeviceId;
            o.Result = PlaybackResult.Success;
            return o;
        }).ToList();

        _dataStore.Setup(d => d.GetOutcomesByDevice(client.DeviceId, 500)).Returns(outcomes);

        _service.RecalibrateClient(client);

        Assert.True(client.CodecConfidence["hevc"] > 0.5);
    }

    [Fact]
    public void RecalibrateClient_TooFewOutcomes_SkipsCodec()
    {
        var client = Client();
        client.CodecConfidence["vp9"] = 0.3;

        var outcomes = new List<PlaybackOutcome>
        {
            SuccessOutcome("DirectPlay", "vp9", "opus", "webm"),
        };
        outcomes[0].DeviceId = client.DeviceId;
        outcomes[0].Result = PlaybackResult.Success;

        _dataStore.Setup(d => d.GetOutcomesByDevice(client.DeviceId, 500)).Returns(outcomes);

        _service.RecalibrateClient(client);

        Assert.Equal(0.3, client.CodecConfidence["vp9"]);
    }

    [Fact]
    public void RecalibrateClient_MixedResults_BlendedConfidence()
    {
        var client = Client();
        client.CodecConfidence["h264"] = 0.9;

        var outcomes = new List<PlaybackOutcome>();
        for (int i = 0; i < 4; i++)
        {
            var o = SuccessOutcome("DirectPlay", "h264", "aac", "mp4");
            o.DeviceId = client.DeviceId;
            o.Result = PlaybackResult.Success;
            outcomes.Add(o);
        }

        var fail = SuccessOutcome("Transcode", "h264", "aac", "mp4");
        fail.DeviceId = client.DeviceId;
        fail.Result = PlaybackResult.Transcoded;
        outcomes.Add(fail);

        _dataStore.Setup(d => d.GetOutcomesByDevice(client.DeviceId, 500)).Returns(outcomes);

        _service.RecalibrateClient(client);

        double confidence = client.CodecConfidence["h264"];
        Assert.True(confidence > 0.5 && confidence < 0.95);
    }

    [Fact]
    public void RecalibrateClient_NoOutcomes_NoChange()
    {
        var client = Client();
        client.CodecConfidence["h264"] = 0.7;

        _dataStore.Setup(d => d.GetOutcomesByDevice(client.DeviceId, 500))
                  .Returns(Enumerable.Empty<PlaybackOutcome>());

        _service.RecalibrateClient(client);

        Assert.Equal(0.7, client.CodecConfidence["h264"]);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static ClientModel Client() => new()
    {
        DeviceId = "test-device",
        ClientName = "TestClient",
        ClientType = ClientType.Desktop,
    };

    private static PlaybackOutcome Outcome(string playMethod, long? playedTicks, long? totalTicks) => new()
    {
        PlayMethod = playMethod,
        PlayedTicks = playedTicks,
        TotalTicks = totalTicks,
        Result = PlaybackResult.Unknown,
    };

    private static PlaybackOutcome SuccessOutcome(string playMethod, string videoCodec, string audioCodec, string container) => new()
    {
        PlayMethod = playMethod,
        VideoCodec = videoCodec,
        AudioCodec = audioCodec,
        Container = container,
        PlayedTicks = 5000,
        TotalTicks = 10000,
        Result = playMethod.Equals("Transcode", StringComparison.OrdinalIgnoreCase)
            ? PlaybackResult.Transcoded
            : PlaybackResult.Success,
        DeviceId = "test-device",
    };
}
