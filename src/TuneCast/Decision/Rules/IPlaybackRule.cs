using TuneCast.Models;

namespace TuneCast.Decision.Rules;

/// <summary>
/// A static playback rule that encodes a known compatibility fact.
/// Rules are deterministic — same inputs always produce the same output.
/// Return null if the rule doesn't apply to this client+media combination.
/// </summary>
public interface IPlaybackRule
{
    /// <summary>
    /// Gets the unique name of this rule for logging and telemetry.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates whether this rule applies to the given client and media.
    /// </summary>
    /// <param name="client">The resolved client model.</param>
    /// <param name="media">The resolved media model.</param>
    /// <returns>A rule result if the rule applies, or null if it doesn't.</returns>
    RuleResult? Evaluate(ClientModel client, MediaModel media);
}
