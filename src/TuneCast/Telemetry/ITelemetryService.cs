using TuneCast.Models;

namespace TuneCast.Telemetry;

/// <summary>
/// Records playback decisions, outcomes, and events for analysis and learning.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Records a playback start event with the decision that was made.
    /// </summary>
    /// <param name="client">The resolved client model.</param>
    /// <param name="media">The resolved media model.</param>
    /// <param name="policy">The policy that was computed.</param>
    /// <param name="playSessionId">The Jellyfin play session identifier.</param>
    /// <param name="observedPlayMethod">The actual play method Jellyfin selected.</param>
    /// <param name="transcodeReasons">Transcode reason flags if applicable.</param>
    void RecordPlaybackStart(
        ClientModel client,
        MediaModel media,
        PlaybackPolicy policy,
        string playSessionId,
        string observedPlayMethod,
        string transcodeReasons);

    /// <summary>
    /// Records a playback stop event with duration information.
    /// </summary>
    /// <param name="playSessionId">The Jellyfin play session identifier.</param>
    /// <param name="playedTicks">How long playback lasted.</param>
    /// <param name="totalTicks">Total media duration.</param>
    /// <returns>The classified outcome, or null if no matching start record was found.</returns>
    PlaybackOutcome? RecordPlaybackStop(string playSessionId, long? playedTicks, long? totalTicks);

    /// <summary>
    /// Prunes old telemetry data based on configured retention period.
    /// </summary>
    void PruneOldData();
}
