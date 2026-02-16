using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TuneCast.Models;
using TuneCast.Storage;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;

namespace TuneCast.Intelligence;

/// <summary>
/// Resolves client identity through a pipeline of heuristics.
/// Never blindly trusts client claims — builds confidence over time.
/// </summary>
public class ClientIntelligenceService : IClientIntelligenceService
{
    private readonly ConcurrentDictionary<string, ClientModel> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPluginDataStore _dataStore;
    private readonly ILogger<ClientIntelligenceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientIntelligenceService"/> class.
    /// </summary>
    /// <param name="dataStore">Persistent data store.</param>
    /// <param name="logger">Logger instance.</param>
    public ClientIntelligenceService(IPluginDataStore dataStore, ILogger<ClientIntelligenceService> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public ClientModel ResolveClient(SessionInfo session)
    {
        var deviceId = session.DeviceId ?? string.Empty;

        if (_cache.TryGetValue(deviceId, out var cached))
        {
            return cached;
        }

        var stored = _dataStore.GetClientModel(deviceId);
        var resolved = stored ?? new ClientModel { DeviceId = deviceId, FirstSeen = DateTime.UtcNow };

        resolved.ClientName = session.Client ?? string.Empty;
        resolved.ClientVersion = session.ApplicationVersion ?? string.Empty;
        resolved.DeviceName = session.DeviceName ?? string.Empty;
        resolved.ClientType = ResolveClientType(session);
        resolved.LastUpdated = DateTime.UtcNow;

        ApplyDefaultConfidence(resolved);

        _cache[deviceId] = resolved;
        _dataStore.UpsertClientModel(resolved);

        _logger.LogDebug(
            "Resolved client {DeviceId} as {ClientType} ({ClientName} v{Version})",
            deviceId,
            resolved.ClientType,
            resolved.ClientName,
            resolved.ClientVersion);

        return resolved;
    }

    /// <inheritdoc />
    public ClientModel? GetCachedClient(string deviceId)
    {
        _cache.TryGetValue(deviceId, out var model);
        return model;
    }

    /// <inheritdoc />
    public void InvalidateCache(string deviceId)
    {
        _cache.TryRemove(deviceId, out _);
    }

    private static ClientType ResolveClientType(SessionInfo session)
    {
        var clientName = (session.Client ?? string.Empty).ToLowerInvariant();
        var deviceName = (session.DeviceName ?? string.Empty).ToLowerInvariant();

        if (clientName.Contains("roku", StringComparison.Ordinal))
        {
            return ClientType.Roku;
        }

        if (clientName.Contains("swiftfin", StringComparison.Ordinal))
        {
            if (deviceName.Contains("apple tv", StringComparison.Ordinal)
                || deviceName.Contains("appletv", StringComparison.Ordinal))
            {
                return ClientType.SwiftfinTvos;
            }

            return ClientType.SwiftfinIos;
        }

        if (clientName.Contains("android", StringComparison.Ordinal))
        {
            if (clientName.Contains("androidtv", StringComparison.Ordinal)
                || deviceName.Contains("fire tv", StringComparison.Ordinal)
                || deviceName.Contains("firetv", StringComparison.Ordinal)
                || deviceName.Contains("aftm", StringComparison.Ordinal))
            {
                if (deviceName.Contains("fire", StringComparison.Ordinal)
                    || deviceName.Contains("aft", StringComparison.Ordinal))
                {
                    return ClientType.FireTv;
                }

                return ClientType.AndroidTv;
            }

            return ClientType.AndroidMobile;
        }

        if (clientName.Contains("findroid", StringComparison.Ordinal))
        {
            return ClientType.AndroidTv;
        }

        if (clientName.Contains("jellyfin web", StringComparison.Ordinal)
            || clientName.Contains("jellyfin-web", StringComparison.Ordinal))
        {
            return ClientType.WebBrowser;
        }

        if (clientName.Contains("jellyfin media player", StringComparison.Ordinal)
            || clientName.Contains("jellyfin desktop", StringComparison.Ordinal)
            || clientName.Contains("jellyfin mpv", StringComparison.Ordinal))
        {
            return ClientType.Desktop;
        }

        if (clientName.Contains("xbox", StringComparison.Ordinal))
        {
            return ClientType.Xbox;
        }

        if (clientName.Contains("kodi", StringComparison.Ordinal))
        {
            return ClientType.Kodi;
        }

        if (clientName.Contains("dlna", StringComparison.Ordinal))
        {
            return ClientType.Dlna;
        }

        return ClientType.Unknown;
    }

    /// <summary>
    /// Applies baseline codec and container confidence based on known client type capabilities.
    /// Only sets defaults for entries not already in the map (preserves learned values).
    /// </summary>
    private static void ApplyDefaultConfidence(ClientModel client)
    {
        var codecDefaults = GetBaselineCodecConfidence(client.ClientType);
        foreach (var kvp in codecDefaults)
        {
            client.CodecConfidence.TryAdd(kvp.Key, kvp.Value);
        }

        var containerDefaults = GetBaselineContainerConfidence(client.ClientType);
        foreach (var kvp in containerDefaults)
        {
            client.ContainerConfidence.TryAdd(kvp.Key, kvp.Value);
        }
    }

    private static Dictionary<string, double> GetBaselineCodecConfidence(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.WebBrowser => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Video
                ["h264"] = 0.95,
                ["hevc"] = 0.2,    // Most browsers can't decode HEVC
                ["vp8"] = 0.85,
                ["vp9"] = 0.7,     // Chrome/Edge good, Safari limited
                ["av1"] = 0.4,     // Chrome 70+, Firefox 67+, Safari 17+
                ["mpeg2video"] = 0.1,
                ["vc1"] = 0.05,
                // Audio
                ["aac"] = 0.95,
                ["mp3"] = 0.95,
                ["opus"] = 0.8,
                ["vorbis"] = 0.7,
                ["flac"] = 0.6,    // Chrome/Edge only
                ["ac3"] = 0.3,     // No native browser support
                ["eac3"] = 0.1,
                ["truehd"] = 0.0,
                ["dts"] = 0.0,
                ["dts-hd ma"] = 0.0,
                ["dts-hd hra"] = 0.0,
            },
            ClientType.Roku => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Video
                ["h264"] = 0.95,
                ["hevc"] = 0.6,    // Only in MP4/M4V/MOV containers
                ["vp9"] = 0.3,     // Limited model support
                ["av1"] = 0.1,     // Ultra 2024+ only
                ["mpeg2video"] = 0.2,
                ["vc1"] = 0.1,
                // Audio
                ["aac"] = 0.95,
                ["mp3"] = 0.9,
                ["ac3"] = 0.7,     // Roku TVs & Ultra via passthrough
                ["eac3"] = 0.5,    // Roku Ultra and TV models only
                ["opus"] = 0.1,
                ["flac"] = 0.3,
                ["truehd"] = 0.0,
                ["dts"] = 0.1,
                ["dts-hd ma"] = 0.0,
                ["vorbis"] = 0.2,
            },
            ClientType.AndroidTv or ClientType.FireTv => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Video
                ["h264"] = 0.95,
                ["hevc"] = 0.6,    // Device/SoC-dependent
                ["vp9"] = 0.5,     // Hardware-dependent
                ["av1"] = 0.3,     // Android 10+ with HW decoder
                ["mpeg2video"] = 0.4,
                ["vc1"] = 0.3,
                // Audio
                ["aac"] = 0.95,
                ["mp3"] = 0.95,
                ["ac3"] = 0.7,     // Most devices via passthrough
                ["eac3"] = 0.5,    // Shield/high-end devices
                ["truehd"] = 0.3,  // Shield Pro via passthrough
                ["dts"] = 0.5,     // Common passthrough support
                ["dts-hd ma"] = 0.3,
                ["dts-hd hra"] = 0.3,
                ["flac"] = 0.6,
                ["opus"] = 0.5,
                ["vorbis"] = 0.5,
            },
            ClientType.SwiftfinIos or ClientType.SwiftfinTvos => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Video
                ["h264"] = 0.95,
                ["hevc"] = 0.85,   // HW decode on A8X+ / all modern iPhones
                ["vp9"] = 0.4,     // VLCKit player only
                ["av1"] = 0.3,     // A17/M3+ only
                ["mpeg2video"] = 0.3,
                ["vc1"] = 0.1,
                // Audio
                ["aac"] = 0.95,
                ["mp3"] = 0.95,
                ["ac3"] = 0.8,     // Apple TV has good Dolby support
                ["eac3"] = 0.8,    // Dolby Digital Plus
                ["truehd"] = 0.4,  // Apple TV 4K via eARC passthrough
                ["dts"] = 0.2,     // No native Apple support
                ["dts-hd ma"] = 0.1,
                ["flac"] = 0.7,    // ALAC conversion typically transparent
                ["opus"] = 0.3,
                ["vorbis"] = 0.3,
                ["alac"] = 0.95,
            },
            ClientType.Desktop or ClientType.Kodi => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Video — mpv/VLC/Kodi play essentially everything
                ["h264"] = 0.95,
                ["hevc"] = 0.9,
                ["vp8"] = 0.9,
                ["vp9"] = 0.9,
                ["av1"] = 0.7,     // SW decode always available, HW varies
                ["mpeg2video"] = 0.9,
                ["vc1"] = 0.8,
                ["mpeg4"] = 0.9,   // DivX/Xvid
                ["theora"] = 0.8,
                // Audio — full passthrough capable
                ["aac"] = 0.95,
                ["mp3"] = 0.95,
                ["ac3"] = 0.9,
                ["eac3"] = 0.9,
                ["truehd"] = 0.7,  // Passthrough if receiver supports it
                ["dts"] = 0.8,
                ["dts-hd ma"] = 0.7,
                ["dts-hd hra"] = 0.7,
                ["flac"] = 0.95,
                ["opus"] = 0.9,
                ["vorbis"] = 0.9,
                ["alac"] = 0.8,
                ["pcm"] = 0.9,
            },
            ClientType.Xbox => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Video
                ["h264"] = 0.95,
                ["hevc"] = 0.5,    // Series X/S better than One
                ["vp9"] = 0.3,     // Limited
                ["av1"] = 0.1,     // Not yet supported
                ["vc1"] = 0.7,     // Native Xbox format
                ["mpeg2video"] = 0.3,
                // Audio
                ["aac"] = 0.95,
                ["mp3"] = 0.95,
                ["ac3"] = 0.7,
                ["eac3"] = 0.5,
                ["truehd"] = 0.2,  // Via HDMI passthrough only
                ["dts"] = 0.4,
                ["dts-hd ma"] = 0.2,
                ["flac"] = 0.4,
            },
            ClientType.Dlna => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Video — DLNA is wildly inconsistent
                ["h264"] = 0.7,
                ["hevc"] = 0.2,
                ["mpeg2video"] = 0.5,
                ["vc1"] = 0.3,
                // Audio
                ["aac"] = 0.7,
                ["mp3"] = 0.8,
                ["ac3"] = 0.5,
                ["lpcm"] = 0.6,
                ["flac"] = 0.2,
            },
            ClientType.AndroidMobile => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Video
                ["h264"] = 0.95,
                ["hevc"] = 0.5,    // Varies by SoC
                ["vp9"] = 0.5,
                ["av1"] = 0.2,     // Flagship 2023+ only
                // Audio
                ["aac"] = 0.95,
                ["mp3"] = 0.95,
                ["opus"] = 0.6,
                ["ac3"] = 0.3,     // Rarely direct on phone speakers
                ["flac"] = 0.5,
                ["vorbis"] = 0.5,
            },
            _ => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["h264"] = 0.7,
                ["hevc"] = 0.3,
                ["vp9"] = 0.2,
                ["av1"] = 0.1,
                ["aac"] = 0.7,
                ["mp3"] = 0.7,
                ["ac3"] = 0.3,
            }
        };
    }

    private static Dictionary<string, double> GetBaselineContainerConfidence(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.WebBrowser => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.95,
                ["m4v"] = 0.9,
                ["webm"] = 0.85,
                ["mkv"] = 0.2,     // Firefox can't, Chrome partial
                ["avi"] = 0.05,
                ["ts"] = 0.4,      // HLS segments work, raw TS less so
                ["mov"] = 0.7,     // Safari good, others variable
                ["ogg"] = 0.6,
                ["flv"] = 0.05,
                ["wmv"] = 0.0,
                ["mpegts"] = 0.4,
            },
            ClientType.Roku => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.95,
                ["m4v"] = 0.9,
                ["mov"] = 0.8,
                ["mkv"] = 0.1,     // MKV not natively playable
                ["ts"] = 0.5,
                ["hls"] = 0.9,     // HLS is Roku's preferred streaming
                ["avi"] = 0.1,
                ["webm"] = 0.1,
                ["wmv"] = 0.0,
            },
            ClientType.AndroidTv or ClientType.FireTv => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.95,
                ["mkv"] = 0.8,     // Android's MediaCodec handles MKV well
                ["m4v"] = 0.9,
                ["webm"] = 0.7,
                ["ts"] = 0.7,
                ["avi"] = 0.5,
                ["mov"] = 0.8,
                ["ogg"] = 0.5,
                ["flv"] = 0.3,
                ["wmv"] = 0.1,
                ["mpegts"] = 0.7,
            },
            ClientType.SwiftfinIos or ClientType.SwiftfinTvos => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.95,
                ["m4v"] = 0.95,
                ["mov"] = 0.95,    // Native Apple container
                ["mkv"] = 0.6,     // VLCKit handles it, native player doesn't
                ["ts"] = 0.7,
                ["webm"] = 0.3,
                ["avi"] = 0.3,
                ["hls"] = 0.95,    // Apple's native streaming format
                ["wmv"] = 0.0,
            },
            ClientType.Desktop or ClientType.Kodi => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.95,
                ["mkv"] = 0.95,    // mpv/VLC/Kodi eat everything
                ["m4v"] = 0.95,
                ["webm"] = 0.95,
                ["ts"] = 0.9,
                ["avi"] = 0.9,
                ["mov"] = 0.95,
                ["ogg"] = 0.9,
                ["flv"] = 0.8,
                ["wmv"] = 0.7,
                ["mpegts"] = 0.9,
            },
            ClientType.Xbox => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.95,
                ["m4v"] = 0.8,
                ["mkv"] = 0.5,
                ["avi"] = 0.4,
                ["ts"] = 0.5,
                ["mov"] = 0.6,
                ["wmv"] = 0.7,     // Native Microsoft format
                ["webm"] = 0.2,
            },
            ClientType.Dlna => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.7,
                ["ts"] = 0.6,      // DLNA often uses MPEG-TS
                ["mpegts"] = 0.6,
                ["avi"] = 0.4,
                ["mkv"] = 0.1,     // Most DLNA renderers choke on MKV
                ["wmv"] = 0.3,
                ["mov"] = 0.3,
            },
            ClientType.AndroidMobile => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.95,
                ["mkv"] = 0.7,
                ["m4v"] = 0.9,
                ["webm"] = 0.6,
                ["ts"] = 0.5,
                ["avi"] = 0.4,
                ["mov"] = 0.7,
                ["ogg"] = 0.5,
            },
            _ => new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["mp4"] = 0.7,
                ["mkv"] = 0.3,
                ["ts"] = 0.4,
                ["avi"] = 0.2,
                ["mov"] = 0.4,
            }
        };
    }
}
