using System;
using System.Collections.Concurrent;
using System.Linq;
using TuneCast.Models;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace TuneCast.Intelligence;

/// <summary>
/// Analyzes media characteristics and estimates transcode cost.
/// Does NOT perform expensive media probing — uses Jellyfin's existing metadata.
/// </summary>
public class MediaIntelligenceService : IMediaIntelligenceService
{
    private readonly ConcurrentDictionary<string, MediaModel> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<MediaIntelligenceService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaIntelligenceService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public MediaIntelligenceService(ILogger<MediaIntelligenceService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public MediaModel ResolveMedia(MediaSourceInfo mediaSource, string itemId)
    {
        var sourceId = mediaSource.Id ?? string.Empty;

        if (_cache.TryGetValue(sourceId, out var cached))
        {
            return cached;
        }

        var videoStream = mediaSource.MediaStreams?
            .FirstOrDefault(s => s.Type == MediaStreamType.Video);

        var audioStream = mediaSource.MediaStreams?
            .FirstOrDefault(s => s.Type == MediaStreamType.Audio && !s.IsExternal);

        var hasImageSubs = mediaSource.MediaStreams?
            .Any(s => s.Type == MediaStreamType.Subtitle && !s.IsTextSubtitleStream) ?? false;

        var hasTextSubs = mediaSource.MediaStreams?
            .Any(s => s.Type == MediaStreamType.Subtitle && s.IsTextSubtitleStream) ?? false;

        var model = new MediaModel
        {
            MediaSourceId = sourceId,
            ItemId = itemId,
            VideoCodec = (videoStream?.Codec ?? string.Empty).ToLowerInvariant(),
            AudioCodec = (audioStream?.Codec ?? string.Empty).ToLowerInvariant(),
            Container = (mediaSource.Container ?? string.Empty).ToLowerInvariant(),
            Bitrate = mediaSource.Bitrate.HasValue ? (int)mediaSource.Bitrate.Value : null,
            Width = videoStream?.Width,
            Height = videoStream?.Height,
            VideoBitDepth = videoStream?.BitDepth,
            VideoProfile = videoStream?.Profile ?? string.Empty,
            VideoRangeType = videoStream is not null ? videoStream.VideoRangeType.ToString() : string.Empty,
            AudioChannels = audioStream?.Channels,
            HasImageSubtitles = hasImageSubs,
            HasTextSubtitles = hasTextSubs,
        };

        model.TranscodeCostEstimate = EstimateTranscodeCost(model);

        _cache[sourceId] = model;

        _logger.LogDebug(
            "Resolved media {SourceId}: {VideoCodec}/{AudioCodec} in {Container}, {Width}x{Height}, cost={Cost}",
            sourceId,
            model.VideoCodec,
            model.AudioCodec,
            model.Container,
            model.Width,
            model.Height,
            model.TranscodeCostEstimate);

        return model;
    }

    /// <inheritdoc />
    public MediaModel? GetCachedMedia(string mediaSourceId)
    {
        _cache.TryGetValue(mediaSourceId, out var model);
        return model;
    }

    /// <inheritdoc />
    public void InvalidateAllCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Heuristic transcode cost estimation covering the full codec/container spectrum.
    /// This is trade-off math, not dogma. Scores accumulate — multiple cost factors compound.
    /// </summary>
    internal static TranscodeCost EstimateTranscodeCost(MediaModel media)
    {
        bool is4K = media.Width >= 3840 || media.Height >= 2160;
        bool is1440P = !is4K && (media.Width >= 2560 || media.Height >= 1440);
        bool isHdr = !string.IsNullOrEmpty(media.VideoRangeType)
                     && !media.VideoRangeType.Equals("SDR", StringComparison.OrdinalIgnoreCase);
        bool isDolbyVision = media.VideoRangeType.Contains("DoVi", StringComparison.OrdinalIgnoreCase)
                             || media.VideoRangeType.Contains("DolbyVision", StringComparison.OrdinalIgnoreCase)
                             || media.VideoRangeType.Contains("Dolby Vision", StringComparison.OrdinalIgnoreCase);
        bool is10Bit = media.VideoBitDepth.HasValue && media.VideoBitDepth.Value >= 10;
        bool is12Bit = media.VideoBitDepth.HasValue && media.VideoBitDepth.Value >= 12;

        int costScore = 0;

        costScore += ClassifyVideoCodecWeight(media.VideoCodec);

        if (is4K) costScore += 3;
        else if (is1440P) costScore += 1;

        if (isDolbyVision) costScore += 4;
        else if (isHdr) costScore += 2;

        if (is12Bit) costScore += 2;
        else if (is10Bit) costScore += 1;

        if (media.HasImageSubtitles) costScore += 2;

        costScore += ClassifyAudioTranscodeWeight(media.AudioCodec, media.AudioChannels);

        if (costScore <= 0)
        {
            return EvaluateRemuxPotential(media);
        }

        if (costScore <= 1)
        {
            return TranscodeCost.Low;
        }

        if (costScore <= 3)
        {
            return TranscodeCost.Medium;
        }

        if (costScore <= 6)
        {
            return TranscodeCost.High;
        }

        return TranscodeCost.Extreme;
    }

    /// <summary>
    /// Assigns a base weight to the video codec based on decode complexity.
    /// </summary>
    private static int ClassifyVideoCodecWeight(string videoCodec)
    {
        if (string.IsNullOrEmpty(videoCodec))
        {
            return 0;
        }

        return videoCodec.ToLowerInvariant() switch
        {
            "h264" or "avc" => 0,              // Universally supported — baseline
            "mpeg2video" or "mpeg2" => 0,       // Old, cheap to decode, but rarely direct-playable
            "mpeg4" => 0,                       // DivX/Xvid — legacy, cheap
            "vp8" => 0,                         // Lightweight
            "theora" => 0,                      // Lightweight
            "hevc" or "h265" => 1,              // Common but not universal
            "vp9" => 1,                         // Similar decode complexity to HEVC
            "av1" => 2,                         // Heavy SW decode, HW decode still limited
            "vc1" or "wmv3" => 1,               // Windows Media — niche HW support
            _ => 1,                             // Unknown codec — assume non-trivial
        };
    }

    /// <summary>
    /// Evaluates whether audio alone would force a transcode and how expensive it would be.
    /// High channel counts with complex codecs are more expensive.
    /// </summary>
    private static int ClassifyAudioTranscodeWeight(string audioCodec, int? channels)
    {
        if (string.IsNullOrEmpty(audioCodec))
        {
            return 0;
        }

        int baseWeight = audioCodec.ToLowerInvariant() switch
        {
            "aac" or "mp3" or "mp2" or "opus" or "vorbis" or "flac" or "alac" or "pcm" or "lpcm" => 0,
            "ac3" => 0,                         // Widely supported via passthrough
            "eac3" => 0,                        // Dolby Digital Plus — common passthrough
            "truehd" => 1,                      // Atmos carrier — limited device support
            "dts" => 0,                         // Base DTS — decent support
            "dts-hd ma" or "dts-hd hra" => 1,   // Lossless DTS — limited direct support
            _ => 0,
        };

        if (channels.HasValue && channels.Value > 6 && baseWeight > 0)
        {
            baseWeight += 1;
        }

        return baseWeight;
    }

    /// <summary>
    /// For media that scores 0 on codec complexity, check if a container remux is likely needed.
    /// </summary>
    private static TranscodeCost EvaluateRemuxPotential(MediaModel media)
    {
        if (string.IsNullOrEmpty(media.VideoCodec) || string.IsNullOrEmpty(media.Container))
        {
            return TranscodeCost.Low;
        }

        bool isUniversalContainer = media.Container.Equals("mp4", StringComparison.OrdinalIgnoreCase)
                                    || media.Container.Equals("m4v", StringComparison.OrdinalIgnoreCase)
                                    || media.Container.Equals("mov", StringComparison.OrdinalIgnoreCase);

        if (isUniversalContainer)
        {
            return TranscodeCost.Low;
        }

        bool isMkvWithCompatibleCodec = media.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase)
                                        && (media.VideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase)
                                            || media.VideoCodec.Equals("hevc", StringComparison.OrdinalIgnoreCase)
                                            || media.VideoCodec.Equals("h265", StringComparison.OrdinalIgnoreCase));

        if (isMkvWithCompatibleCodec)
        {
            return TranscodeCost.Remux;
        }

        bool isLegacyContainer = media.Container.Equals("avi", StringComparison.OrdinalIgnoreCase)
                                 || media.Container.Equals("wmv", StringComparison.OrdinalIgnoreCase)
                                 || media.Container.Equals("flv", StringComparison.OrdinalIgnoreCase);

        if (isLegacyContainer)
        {
            return TranscodeCost.Remux;
        }

        return TranscodeCost.Low;
    }
}
