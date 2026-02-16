using System;
using System.Collections.Generic;

namespace TuneCast.Models;

/// <summary>
/// Normalized representation of a client device's capabilities and reliability.
/// Decoupled from Jellyfin types for loose coupling and upgrade survival.
/// </summary>
public class ClientModel
{
    /// <summary>
    /// Gets or sets the unique device identifier (from Jellyfin session).
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resolved client type.
    /// </summary>
    public ClientType ClientType { get; set; } = ClientType.Unknown;

    /// <summary>
    /// Gets or sets the client application name (e.g. "Jellyfin Web", "Jellyfin for Roku").
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client application version string.
    /// </summary>
    public string ClientVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device name as reported by the client.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw user agent string.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets codec support confidence scores.
    /// Key: lowercase codec name (e.g. "hevc", "h264", "av1").
    /// Value: confidence 0.0 (no support) to 1.0 (confirmed support).
    /// </summary>
    public Dictionary<string, double> CodecConfidence { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets container support confidence scores.
    /// Key: lowercase container name (e.g. "mkv", "mp4", "ts").
    /// Value: confidence 0.0 (no support) to 1.0 (confirmed support).
    /// </summary>
    public Dictionary<string, double> ContainerConfidence { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the maximum bitrate the client can reliably handle (bits/sec).
    /// Null means unknown / unconstrained.
    /// </summary>
    public int? MaxBitrate { get; set; }

    /// <summary>
    /// Gets or sets the overall reliability score for this client.
    /// Range: 0.0 (unreliable) to 1.0 (rock solid).
    /// </summary>
    public double ReliabilityScore { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets when this model was first created.
    /// </summary>
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when this model was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Known client platform categories.
/// </summary>
public enum ClientType
{
    /// <summary>Unidentified client.</summary>
    Unknown = 0,

    /// <summary>Web browser (Chrome, Firefox, Safari, Edge).</summary>
    WebBrowser,

    /// <summary>Android TV official client or Findroid.</summary>
    AndroidTv,

    /// <summary>Android mobile client.</summary>
    AndroidMobile,

    /// <summary>Roku channel.</summary>
    Roku,

    /// <summary>Amazon Fire TV (Android TV variant).</summary>
    FireTv,

    /// <summary>Swiftfin on iOS.</summary>
    SwiftfinIos,

    /// <summary>Swiftfin on tvOS / Apple TV.</summary>
    SwiftfinTvos,

    /// <summary>Jellyfin Desktop (formerly Jellyfin Media Player).</summary>
    Desktop,

    /// <summary>Xbox app.</summary>
    Xbox,

    /// <summary>Kodi with Jellyfin plugin.</summary>
    Kodi,

    /// <summary>DLNA renderer.</summary>
    Dlna
}
