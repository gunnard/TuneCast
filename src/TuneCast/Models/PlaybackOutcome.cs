using System;

namespace TuneCast.Models;

/// <summary>
/// Records the outcome of a playback session for telemetry and learning.
/// </summary>
public class PlaybackOutcome
{
    /// <summary>
    /// Gets or sets the LiteDB document identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client application name.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin item identifier.
    /// </summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin play session identifier.
    /// </summary>
    public string PlaySessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the video codec of the media source.
    /// </summary>
    public string VideoCodec { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the audio codec of the media source.
    /// </summary>
    public string AudioCodec { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the container format.
    /// </summary>
    public string Container { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the play method observed (DirectPlay, DirectStream, Transcode).
    /// </summary>
    public string PlayMethod { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transcode reasons if transcoding occurred.
    /// Comma-separated flags from Jellyfin's TranscodeReason enum.
    /// </summary>
    public string TranscodeReasons { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the result of the playback session.
    /// </summary>
    public PlaybackResult Result { get; set; } = PlaybackResult.Unknown;

    /// <summary>
    /// Gets or sets the playback duration in ticks before the session ended.
    /// </summary>
    public long? PlayedTicks { get; set; }

    /// <summary>
    /// Gets or sets the total media runtime in ticks.
    /// </summary>
    public long? TotalTicks { get; set; }

    /// <summary>
    /// Gets or sets the policy that was active during this session (serialized).
    /// </summary>
    public string PolicySnapshot { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this playback session started.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Observed result of a playback session.
/// </summary>
public enum PlaybackResult
{
    /// <summary>Outcome not yet determined.</summary>
    Unknown = 0,

    /// <summary>Playback completed or user stopped normally.</summary>
    Success,

    /// <summary>Playback failed or errored.</summary>
    Failure,

    /// <summary>Session was very short relative to media length — possible failure or buffering.</summary>
    SuspectedFailure,

    /// <summary>Transcoding was triggered (not necessarily a failure, but noteworthy).</summary>
    Transcoded
}
