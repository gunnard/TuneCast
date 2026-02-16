using System;

namespace TuneCast.Models;

/// <summary>
/// Records a policy intervention — when TuneCast influenced (or would have influenced)
/// a playback decision for a client/media combination.
/// </summary>
public class InterventionRecord
{
    /// <summary>
    /// Gets or sets the unique identifier for this record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the intervention.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client/device display name.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client application name.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the media source identifier.
    /// </summary>
    public string MediaSourceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether a profile was actively applied or just a dry run.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets whether direct play was allowed by the policy.
    /// </summary>
    public bool AllowDirectPlay { get; set; }

    /// <summary>
    /// Gets or sets whether direct stream was allowed by the policy.
    /// </summary>
    public bool AllowDirectStream { get; set; }

    /// <summary>
    /// Gets or sets whether transcoding was allowed by the policy.
    /// </summary>
    public bool AllowTranscoding { get; set; }

    /// <summary>
    /// Gets or sets the bitrate cap applied, if any.
    /// </summary>
    public int? BitrateCap { get; set; }

    /// <summary>
    /// Gets or sets the confidence level of the policy decision (0.0–1.0).
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the human-readable reasoning for the decision.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;
}
