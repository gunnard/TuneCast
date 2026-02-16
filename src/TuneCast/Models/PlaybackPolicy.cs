namespace TuneCast.Models;

/// <summary>
/// The decision engine's recommendation for a given client + media combination.
/// These are advisory — they bias Jellyfin's native logic, not override it.
/// </summary>
public class PlaybackPolicy
{
    /// <summary>
    /// Gets or sets a value indicating whether direct play should be allowed/favored.
    /// </summary>
    public bool AllowDirectPlay { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether direct stream (remux) should be allowed.
    /// </summary>
    public bool AllowDirectStream { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether transcoding should be allowed as fallback.
    /// </summary>
    public bool AllowTranscoding { get; set; } = true;

    /// <summary>
    /// Gets or sets the recommended max bitrate cap in bits/sec.
    /// Null means no override.
    /// </summary>
    public int? BitrateCap { get; set; }

    /// <summary>
    /// Gets or sets the preferred video codec bias (e.g. prefer "h264" over "hevc" for this client).
    /// Empty means no bias.
    /// </summary>
    public string PreferredVideoCodec { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the confidence level of this policy decision.
    /// Range: 0.0 (pure guess) to 1.0 (high confidence).
    /// </summary>
    public double Confidence { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets the human-readable reasoning for this decision.
    /// Used for telemetry and debugging.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this policy is a default pass-through
    /// that requires no profile shaping.
    /// </summary>
    public bool IsDefault =>
        AllowDirectPlay && AllowDirectStream && AllowTranscoding
        && !BitrateCap.HasValue && Confidence <= 0.0;

    /// <summary>
    /// Creates a default pass-through policy that defers entirely to Jellyfin.
    /// </summary>
    /// <returns>A permissive default policy.</returns>
    public static PlaybackPolicy Default() => new()
    {
        AllowDirectPlay = true,
        AllowDirectStream = true,
        AllowTranscoding = true,
        Confidence = 0.0,
        Reasoning = "Default pass-through — no plugin influence."
    };
}
