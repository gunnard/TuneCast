using System;
using System.Collections.Generic;
using Jellyfin.Data.Enums;
using TuneCast.Models;
using MediaBrowser.Model.Dlna;

namespace TuneCast.Profiles;

/// <summary>
/// Pure function: builds a shaped <see cref="DeviceProfile"/> from a <see cref="PlaybackPolicy"/>
/// and <see cref="ClientModel"/>. No side effects, fully testable.
/// </summary>
public static class ProfileBuilder
{
    /// <summary>
    /// Builds a <see cref="DeviceProfile"/> that encodes the policy decisions
    /// into Jellyfin's native profile format.
    /// </summary>
    /// <param name="client">The resolved client model.</param>
    /// <param name="policy">The computed playback policy.</param>
    /// <returns>A shaped device profile, or null if the policy is default/no-op.</returns>
    public static DeviceProfile? Build(ClientModel client, PlaybackPolicy policy)
    {
        if (policy.IsDefault)
        {
            return null;
        }

        var profile = new DeviceProfile
        {
            Name = $"TuneCast-{client.ClientType}",
            MaxStreamingBitrate = policy.BitrateCap,
            MaxStaticBitrate = policy.BitrateCap,
            DirectPlayProfiles = BuildDirectPlayProfiles(client, policy),
            TranscodingProfiles = BuildTranscodingProfiles(client, policy),
        };

        return profile;
    }

    private static DirectPlayProfile[] BuildDirectPlayProfiles(ClientModel client, PlaybackPolicy policy)
    {
        if (!policy.AllowDirectPlay)
        {
            return Array.Empty<DirectPlayProfile>();
        }

        var profiles = new List<DirectPlayProfile>();

        var videoContainers = GetSupportedVideoContainers(client.ClientType);
        var videoCodecs = GetSupportedVideoCodecs(client.ClientType);
        var audioCodecs = GetSupportedAudioCodecs(client.ClientType);

        if (videoContainers.Length > 0 && videoCodecs.Length > 0)
        {
            profiles.Add(new DirectPlayProfile
            {
                Container = string.Join(",", videoContainers),
                VideoCodec = string.Join(",", videoCodecs),
                AudioCodec = string.Join(",", audioCodecs),
                Type = DlnaProfileType.Video,
            });
        }

        profiles.Add(new DirectPlayProfile
        {
            Container = string.Join(",", GetSupportedAudioContainers()),
            AudioCodec = string.Join(",", audioCodecs),
            Type = DlnaProfileType.Audio,
        });

        return profiles.ToArray();
    }

    private static TranscodingProfile[] BuildTranscodingProfiles(ClientModel client, PlaybackPolicy policy)
    {
        if (!policy.AllowTranscoding && !policy.AllowDirectStream)
        {
            return Array.Empty<TranscodingProfile>();
        }

        var profiles = new List<TranscodingProfile>();

        if (policy.AllowDirectStream || policy.AllowTranscoding)
        {
            profiles.Add(new TranscodingProfile
            {
                Container = "ts",
                VideoCodec = "h264",
                AudioCodec = "aac,ac3",
                Type = DlnaProfileType.Video,
                Context = EncodingContext.Streaming,
                Protocol = MediaStreamProtocol.hls,
                EstimateContentLength = false,
                CopyTimestamps = true,
            });

            profiles.Add(new TranscodingProfile
            {
                Container = "mp4",
                VideoCodec = "h264",
                AudioCodec = "aac",
                Type = DlnaProfileType.Video,
                Context = EncodingContext.Static,
                Protocol = MediaStreamProtocol.http,
            });
        }

        profiles.Add(new TranscodingProfile
        {
            Container = "mp3",
            AudioCodec = "mp3",
            Type = DlnaProfileType.Audio,
            Context = EncodingContext.Streaming,
            Protocol = MediaStreamProtocol.http,
            EstimateContentLength = false,
        });

        return profiles.ToArray();
    }

    private static string[] GetSupportedVideoContainers(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.WebBrowser => new[] { "mp4", "webm" },
            ClientType.Roku => new[] { "mp4", "m4v", "mov" },
            ClientType.AndroidTv or ClientType.FireTv => new[] { "mp4", "mkv", "m4v" },
            ClientType.SwiftfinIos or ClientType.SwiftfinTvos => new[] { "mp4", "m4v", "mov", "mkv" },
            ClientType.Xbox => new[] { "mp4", "m4v", "mov" },
            ClientType.Desktop or ClientType.Kodi => new[] { "mp4", "mkv", "m4v", "mov", "avi", "webm", "ts" },
            ClientType.Dlna => new[] { "mp4", "ts", "mpeg" },
            ClientType.AndroidMobile => new[] { "mp4", "mkv", "m4v" },
            _ => new[] { "mp4" },
        };
    }

    private static string[] GetSupportedVideoCodecs(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.WebBrowser => new[] { "h264", "vp9", "av1" },
            ClientType.Roku => new[] { "h264", "hevc" },
            ClientType.AndroidTv or ClientType.FireTv => new[] { "h264", "hevc", "vp9", "av1" },
            ClientType.SwiftfinIos or ClientType.SwiftfinTvos => new[] { "h264", "hevc" },
            ClientType.Xbox => new[] { "h264", "hevc", "vp9" },
            ClientType.Desktop or ClientType.Kodi => new[] { "h264", "hevc", "vp9", "av1", "mpeg2video", "vc1" },
            ClientType.Dlna => new[] { "h264", "mpeg2video" },
            ClientType.AndroidMobile => new[] { "h264", "hevc", "vp9" },
            _ => new[] { "h264" },
        };
    }

    private static string[] GetSupportedAudioCodecs(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.WebBrowser => new[] { "aac", "mp3", "opus", "flac", "vorbis", "ac3", "eac3" },
            ClientType.Roku => new[] { "aac", "mp3", "ac3", "eac3" },
            ClientType.AndroidTv or ClientType.FireTv => new[] { "aac", "mp3", "ac3", "eac3", "dts", "flac", "opus", "vorbis", "truehd" },
            ClientType.SwiftfinIos or ClientType.SwiftfinTvos => new[] { "aac", "mp3", "ac3", "eac3", "flac", "alac", "opus" },
            ClientType.Xbox => new[] { "aac", "mp3", "ac3", "eac3", "dts", "flac" },
            ClientType.Desktop or ClientType.Kodi => new[] { "aac", "mp3", "ac3", "eac3", "dts", "truehd", "flac", "alac", "opus", "vorbis", "pcm" },
            ClientType.Dlna => new[] { "aac", "mp3", "ac3" },
            ClientType.AndroidMobile => new[] { "aac", "mp3", "ac3", "eac3", "opus", "vorbis", "flac" },
            _ => new[] { "aac", "mp3" },
        };
    }

    private static string[] GetSupportedAudioContainers()
    {
        return new[] { "mp3", "aac", "flac", "ogg", "wav", "m4a" };
    }
}
