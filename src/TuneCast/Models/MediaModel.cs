namespace TuneCast.Models;

/// <summary>
/// Normalized representation of a media source's characteristics.
/// Decoupled from Jellyfin types for loose coupling.
/// </summary>
public class MediaModel
{
    /// <summary>
    /// Gets or sets the Jellyfin media source identifier.
    /// </summary>
    public string MediaSourceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin item identifier.
    /// </summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the video codec (e.g. "hevc", "h264", "av1").
    /// </summary>
    public string VideoCodec { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the primary audio codec (e.g. "aac", "ac3", "truehd").
    /// </summary>
    public string AudioCodec { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the container format (e.g. "mkv", "mp4", "ts").
    /// </summary>
    public string Container { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total bitrate in bits/sec.
    /// </summary>
    public int? Bitrate { get; set; }

    /// <summary>
    /// Gets or sets the video width in pixels.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets the video height in pixels.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets the video bit depth (8, 10, 12).
    /// </summary>
    public int? VideoBitDepth { get; set; }

    /// <summary>
    /// Gets or sets the video profile (e.g. "Main", "Main 10", "High").
    /// </summary>
    public string VideoProfile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the video range type (e.g. "SDR", "HDR10", "HLG", "DolbyVision").
    /// </summary>
    public string VideoRangeType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of audio channels.
    /// </summary>
    public int? AudioChannels { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the media has image-based subtitles (PGS, VobSub).
    /// </summary>
    public bool HasImageSubtitles { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the media has text-based subtitles (SRT, ASS).
    /// </summary>
    public bool HasTextSubtitles { get; set; }

    /// <summary>
    /// Gets or sets the estimated transcoding cost for this media item.
    /// </summary>
    public TranscodeCost TranscodeCostEstimate { get; set; } = TranscodeCost.Unknown;
}

/// <summary>
/// Heuristic estimate of how expensive transcoding this media would be.
/// </summary>
public enum TranscodeCost
{
    /// <summary>Not yet estimated.</summary>
    Unknown = 0,

    /// <summary>Container remux only — negligible CPU.</summary>
    Remux,

    /// <summary>Audio transcode only — low CPU.</summary>
    Low,

    /// <summary>Standard video transcode (1080p H.264).</summary>
    Medium,

    /// <summary>Heavy transcode (4K, HDR tone-mapping, burn-in subs).</summary>
    High,

    /// <summary>Extreme transcode (4K HEVC HDR → H.264 SDR with burn-in subs).</summary>
    Extreme
}
