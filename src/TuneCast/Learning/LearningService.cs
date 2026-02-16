using System;
using System.Collections.Generic;
using System.Linq;
using TuneCast.Configuration;
using TuneCast.Models;
using TuneCast.Storage;
using Microsoft.Extensions.Logging;

namespace TuneCast.Learning;

/// <summary>
/// Adjusts client confidence scores based on observed playback outcomes.
/// Uses exponential moving average — recent outcomes weigh more than old ones.
/// All adjustments are bounded to prevent runaway confidence drift.
/// </summary>
public class LearningService : ILearningService
{
    private const double LearningRate = 0.15;
    private const double MinConfidence = 0.0;
    private const double MaxConfidence = 1.0;
    private const double SuccessBoost = 0.08;
    private const double FailurePenalty = 0.12;
    private const double TranscodePenalty = 0.05;
    private const double SuspectedFailurePenalty = 0.08;
    private const double MinPlaybackRatioForSuccess = 0.15;

    private readonly IPluginDataStore _dataStore;
    private readonly ILogger<LearningService> _logger;
    private readonly Func<PluginConfiguration> _configProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningService"/> class.
    /// Used by Jellyfin DI.
    /// </summary>
    /// <param name="dataStore">Persistent data store for outcome history.</param>
    /// <param name="logger">Logger instance.</param>
    public LearningService(IPluginDataStore dataStore, ILogger<LearningService> logger)
        : this(dataStore, logger, () => Plugin.Instance?.Configuration ?? new PluginConfiguration())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LearningService"/> class.
    /// Used by tests — accepts explicit config provider.
    /// </summary>
    /// <param name="dataStore">Persistent data store for outcome history.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="configProvider">Function that supplies the current configuration.</param>
    internal LearningService(IPluginDataStore dataStore, ILogger<LearningService> logger, Func<PluginConfiguration> configProvider)
    {
        _dataStore = dataStore;
        _logger = logger;
        _configProvider = configProvider;
    }

    /// <inheritdoc />
    public void ProcessOutcome(PlaybackOutcome outcome, ClientModel client)
    {
        if (!_configProvider().EnableLearning)
        {
            return;
        }

        ClassifyOutcome(outcome);

        double videoAdjustment = ComputeVideoCodecAdjustment(outcome);
        double audioAdjustment = ComputeAudioCodecAdjustment(outcome);
        double containerAdjustment = ComputeContainerAdjustment(outcome);

        ApplyConfidenceAdjustment(client.CodecConfidence, outcome.VideoCodec, videoAdjustment);
        ApplyConfidenceAdjustment(client.CodecConfidence, outcome.AudioCodec, audioAdjustment);
        ApplyConfidenceAdjustment(client.ContainerConfidence, outcome.Container, containerAdjustment);

        client.LastUpdated = DateTime.UtcNow;

        _dataStore.UpsertClientModel(client);

        _logger.LogDebug(
            "Learning update for {DeviceId}: video={VideoCodec}({VideoAdj:+0.00;-0.00}), " +
            "audio={AudioCodec}({AudioAdj:+0.00;-0.00}), container={Container}({ContainerAdj:+0.00;-0.00}), " +
            "result={Result}, method={Method}",
            client.DeviceId,
            outcome.VideoCodec, videoAdjustment,
            outcome.AudioCodec, audioAdjustment,
            outcome.Container, containerAdjustment,
            outcome.Result,
            outcome.PlayMethod);
    }

    /// <inheritdoc />
    public void RecalibrateClient(ClientModel client)
    {
        var outcomes = _dataStore.GetOutcomesByDevice(client.DeviceId, 500).ToList();
        if (outcomes.Count == 0)
        {
            return;
        }

        var videoCodecStats = new Dictionary<string, (int success, int total)>(StringComparer.OrdinalIgnoreCase);
        var audioCodecStats = new Dictionary<string, (int success, int total)>(StringComparer.OrdinalIgnoreCase);
        var containerStats = new Dictionary<string, (int success, int total)>(StringComparer.OrdinalIgnoreCase);

        foreach (var outcome in outcomes)
        {
            ClassifyOutcome(outcome);

            bool isSuccess = outcome.Result == PlaybackResult.Success
                             && outcome.PlayMethod.Equals("DirectPlay", StringComparison.OrdinalIgnoreCase);

            AccumulateStats(videoCodecStats, outcome.VideoCodec, isSuccess);
            AccumulateStats(audioCodecStats, outcome.AudioCodec, isSuccess);
            AccumulateStats(containerStats, outcome.Container, isSuccess);
        }

        ApplyRecalibratedConfidence(client.CodecConfidence, videoCodecStats);
        ApplyRecalibratedConfidence(client.CodecConfidence, audioCodecStats);
        ApplyRecalibratedConfidence(client.ContainerConfidence, containerStats);

        client.LastUpdated = DateTime.UtcNow;
        _dataStore.UpsertClientModel(client);

        _logger.LogInformation(
            "Recalibrated {DeviceId} from {OutcomeCount} outcomes: " +
            "{VideoCodecs} video codecs, {AudioCodecs} audio codecs, {Containers} containers updated",
            client.DeviceId,
            outcomes.Count,
            videoCodecStats.Count,
            audioCodecStats.Count,
            containerStats.Count);
    }

    /// <summary>
    /// Classifies an outcome's result if it hasn't been classified yet.
    /// Uses playback ratio and play method to determine success/failure.
    /// </summary>
    internal static void ClassifyOutcome(PlaybackOutcome outcome)
    {
        if (outcome.Result != PlaybackResult.Unknown)
        {
            return;
        }

        bool isTranscode = outcome.PlayMethod.Equals("Transcode", StringComparison.OrdinalIgnoreCase);

        if (isTranscode)
        {
            outcome.Result = PlaybackResult.Transcoded;
            return;
        }

        double playbackRatio = ComputePlaybackRatio(outcome);

        if (playbackRatio < 0)
        {
            outcome.Result = PlaybackResult.Unknown;
        }
        else if (playbackRatio < MinPlaybackRatioForSuccess)
        {
            outcome.Result = PlaybackResult.SuspectedFailure;
        }
        else
        {
            outcome.Result = PlaybackResult.Success;
        }
    }

    internal static double ComputePlaybackRatio(PlaybackOutcome outcome)
    {
        if (!outcome.PlayedTicks.HasValue || !outcome.TotalTicks.HasValue || outcome.TotalTicks.Value <= 0)
        {
            return -1.0;
        }

        return (double)outcome.PlayedTicks.Value / outcome.TotalTicks.Value;
    }

    private static double ComputeVideoCodecAdjustment(PlaybackOutcome outcome)
    {
        if (string.IsNullOrEmpty(outcome.VideoCodec))
        {
            return 0.0;
        }

        return outcome.Result switch
        {
            PlaybackResult.Success when IsDirectPlay(outcome) => SuccessBoost,
            PlaybackResult.Success => SuccessBoost * 0.5,
            PlaybackResult.Failure => -FailurePenalty,
            PlaybackResult.SuspectedFailure => -SuspectedFailurePenalty,
            PlaybackResult.Transcoded => -TranscodePenalty,
            _ => 0.0,
        };
    }

    private static double ComputeAudioCodecAdjustment(PlaybackOutcome outcome)
    {
        if (string.IsNullOrEmpty(outcome.AudioCodec))
        {
            return 0.0;
        }

        return outcome.Result switch
        {
            PlaybackResult.Success when IsDirectPlay(outcome) => SuccessBoost * 0.5,
            PlaybackResult.Failure => -FailurePenalty * 0.5,
            PlaybackResult.SuspectedFailure => -SuspectedFailurePenalty * 0.5,
            PlaybackResult.Transcoded when IsAudioTranscodeOnly(outcome) => -TranscodePenalty,
            _ => 0.0,
        };
    }

    private static double ComputeContainerAdjustment(PlaybackOutcome outcome)
    {
        if (string.IsNullOrEmpty(outcome.Container))
        {
            return 0.0;
        }

        return outcome.Result switch
        {
            PlaybackResult.Success when IsDirectPlay(outcome) => SuccessBoost * 0.5,
            PlaybackResult.Failure => -FailurePenalty * 0.3,
            PlaybackResult.SuspectedFailure => -SuspectedFailurePenalty * 0.3,
            _ => 0.0,
        };
    }

    private static void ApplyConfidenceAdjustment(
        Dictionary<string, double> confidenceMap,
        string key,
        double adjustment)
    {
        if (string.IsNullOrEmpty(key) || adjustment == 0.0)
        {
            return;
        }

        confidenceMap.TryGetValue(key, out double current);
        double updated = Math.Clamp(current + (adjustment * LearningRate), MinConfidence, MaxConfidence);
        confidenceMap[key] = updated;
    }

    private static void AccumulateStats(
        Dictionary<string, (int success, int total)> stats,
        string key,
        bool isSuccess)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        stats.TryGetValue(key, out var current);
        stats[key] = (current.success + (isSuccess ? 1 : 0), current.total + 1);
    }

    private static void ApplyRecalibratedConfidence(
        Dictionary<string, double> confidenceMap,
        Dictionary<string, (int success, int total)> stats)
    {
        foreach (var kvp in stats)
        {
            if (kvp.Value.total < 3)
            {
                continue;
            }

            double observedRate = (double)kvp.Value.success / kvp.Value.total;

            confidenceMap.TryGetValue(kvp.Key, out double existing);
            double blended = (existing * 0.3) + (observedRate * 0.7);
            confidenceMap[kvp.Key] = Math.Clamp(blended, MinConfidence, MaxConfidence);
        }
    }

    private static bool IsDirectPlay(PlaybackOutcome outcome)
    {
        return outcome.PlayMethod.Equals("DirectPlay", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAudioTranscodeOnly(PlaybackOutcome outcome)
    {
        return outcome.TranscodeReasons.Contains("Audio", StringComparison.OrdinalIgnoreCase)
               && !outcome.TranscodeReasons.Contains("Video", StringComparison.OrdinalIgnoreCase);
    }
}
