using TuneCast.Models;

namespace TuneCast.Learning;

/// <summary>
/// Learns from playback outcomes to adjust client confidence scores over time.
/// The feedback loop: PlaybackOutcome → ConfidenceAdjustment → ClientModel update.
/// </summary>
public interface ILearningService
{
    /// <summary>
    /// Processes a completed playback outcome and adjusts the client's
    /// codec/container confidence scores accordingly.
    /// </summary>
    /// <param name="outcome">The completed playback outcome.</param>
    /// <param name="client">The client model to adjust. Modified in place.</param>
    void ProcessOutcome(PlaybackOutcome outcome, ClientModel client);

    /// <summary>
    /// Recalculates confidence scores for a client from its full outcome history.
    /// Use this for bulk recalibration rather than incremental updates.
    /// </summary>
    /// <param name="client">The client model to recalibrate.</param>
    void RecalibrateClient(ClientModel client);
}
