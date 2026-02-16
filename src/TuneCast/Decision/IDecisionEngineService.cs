using TuneCast.Models;

namespace TuneCast.Decision;

/// <summary>
/// Computes playback policy recommendations from client + media intelligence.
/// Returns advisory recommendations, not commands.
/// </summary>
public interface IDecisionEngineService
{
    /// <summary>
    /// Computes the optimal playback policy for a given client and media combination.
    /// </summary>
    /// <param name="client">Resolved client model.</param>
    /// <param name="media">Resolved media model.</param>
    /// <returns>An advisory playback policy.</returns>
    PlaybackPolicy ComputePolicy(ClientModel client, MediaModel media);
}
