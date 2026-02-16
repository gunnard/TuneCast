using System;
using System.Text.Json;
using TuneCast.Configuration;
using TuneCast.Models;
using TuneCast.Storage;
using Microsoft.Extensions.Logging;

namespace TuneCast.Telemetry;

/// <summary>
/// Records playback events and outcomes to persistent storage.
/// Feeds the learning layer and provides ground truth for optimization tuning.
/// </summary>
public class TelemetryService : ITelemetryService
{
    private readonly IPluginDataStore _dataStore;
    private readonly ILogger<TelemetryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryService"/> class.
    /// </summary>
    /// <param name="dataStore">Persistent data store.</param>
    /// <param name="logger">Logger instance.</param>
    public TelemetryService(IPluginDataStore dataStore, ILogger<TelemetryService> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public void RecordPlaybackStart(
        ClientModel client,
        MediaModel media,
        PlaybackPolicy policy,
        string playSessionId,
        string observedPlayMethod,
        string transcodeReasons)
    {
        var outcome = new PlaybackOutcome
        {
            DeviceId = client.DeviceId,
            ClientName = client.ClientName,
            ItemId = media.ItemId,
            PlaySessionId = playSessionId,
            VideoCodec = media.VideoCodec,
            AudioCodec = media.AudioCodec,
            Container = media.Container,
            PlayMethod = observedPlayMethod,
            TranscodeReasons = transcodeReasons,
            Result = string.Equals(observedPlayMethod, "Transcode", StringComparison.OrdinalIgnoreCase)
                ? PlaybackResult.Transcoded
                : PlaybackResult.Unknown,
            PolicySnapshot = SerializePolicy(policy),
            Timestamp = DateTime.UtcNow,
        };

        _dataStore.RecordOutcome(outcome);

        _logger.LogInformation(
            "Telemetry: {ClientName} ({DeviceId}) playing {VideoCodec}/{AudioCodec} in {Container} → {PlayMethod}" +
            "{TranscodeInfo}",
            client.ClientName,
            client.DeviceId,
            media.VideoCodec,
            media.AudioCodec,
            media.Container,
            observedPlayMethod,
            string.IsNullOrEmpty(transcodeReasons) ? string.Empty : $" (reasons: {transcodeReasons})");
    }

    /// <inheritdoc />
    public PlaybackOutcome? RecordPlaybackStop(string playSessionId, long? playedTicks, long? totalTicks)
    {
        var outcomes = _dataStore.GetOutcomesByDevice(string.Empty, 1000);

        foreach (var outcome in outcomes)
        {
            if (!string.Equals(outcome.PlaySessionId, playSessionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            outcome.PlayedTicks = playedTicks;
            outcome.TotalTicks = totalTicks;
            outcome.Result = ClassifyResult(outcome, playedTicks, totalTicks);

            _dataStore.RecordOutcome(outcome);

            _logger.LogInformation(
                "Telemetry: Session {PlaySessionId} ended — result={Result}, played={Played}/{Total}",
                playSessionId,
                outcome.Result,
                playedTicks,
                totalTicks);

            return outcome;
        }

        _logger.LogDebug("Telemetry: No matching start record for session {PlaySessionId}", playSessionId);
        return null;
    }

    /// <inheritdoc />
    public void PruneOldData()
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var cutoff = DateTime.UtcNow.AddDays(-config.TelemetryRetentionDays);
        _dataStore.PruneOutcomes(cutoff);
    }

    private static PlaybackResult ClassifyResult(PlaybackOutcome outcome, long? playedTicks, long? totalTicks)
    {
        if (!playedTicks.HasValue || !totalTicks.HasValue || totalTicks.Value <= 0)
        {
            return outcome.Result == PlaybackResult.Transcoded
                ? PlaybackResult.Transcoded
                : PlaybackResult.Unknown;
        }

        double playedRatio = (double)playedTicks.Value / totalTicks.Value;

        if (playedRatio < 0.02)
        {
            return PlaybackResult.SuspectedFailure;
        }

        if (playedRatio >= 0.02)
        {
            return outcome.Result == PlaybackResult.Transcoded
                ? PlaybackResult.Transcoded
                : PlaybackResult.Success;
        }

        return PlaybackResult.Success;
    }

    private static string SerializePolicy(PlaybackPolicy policy)
    {
        try
        {
            return JsonSerializer.Serialize(policy);
        }
        catch
        {
            return string.Empty;
        }
    }
}
