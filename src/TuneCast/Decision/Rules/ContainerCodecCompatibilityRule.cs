using System;
using TuneCast.Models;

namespace TuneCast.Decision.Rules;

/// <summary>
/// Encodes known container+codec incompatibilities per client type.
/// These are hard facts — ignoring them causes playback failure.
/// </summary>
public class ContainerCodecCompatibilityRule : IPlaybackRule
{
    /// <inheritdoc />
    public string Name => "ContainerCodecCompatibility";

    /// <inheritdoc />
    public RuleResult? Evaluate(ClientModel client, MediaModel media)
    {
        if (string.IsNullOrEmpty(media.Container) || string.IsNullOrEmpty(media.VideoCodec))
        {
            return null;
        }

        return client.ClientType switch
        {
            ClientType.Roku => EvaluateRoku(media),
            ClientType.WebBrowser => EvaluateWebBrowser(media),
            ClientType.SwiftfinIos or ClientType.SwiftfinTvos => EvaluateSwiftfin(media),
            ClientType.Xbox => EvaluateXbox(media),
            ClientType.Dlna => EvaluateDlna(media),
            _ => null,
        };
    }

    private static RuleResult? EvaluateRoku(MediaModel media)
    {
        bool isMkv = media.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase);
        bool isHevc = media.VideoCodec.Equals("hevc", StringComparison.OrdinalIgnoreCase)
                      || media.VideoCodec.Equals("h265", StringComparison.OrdinalIgnoreCase);

        if (isMkv)
        {
            return new RuleResult
            {
                RuleName = "Roku:MkvNotSupported",
                AllowDirectPlay = false,
                AllowDirectStream = true,
                Reasoning = "Roku cannot natively play MKV containers. Remux to MP4/HLS required.",
                Severity = RuleSeverity.Require,
            };
        }

        if (isHevc && !IsHevcFriendlyContainer(media.Container))
        {
            return new RuleResult
            {
                RuleName = "Roku:HevcContainerMismatch",
                AllowDirectPlay = false,
                AllowDirectStream = true,
                Reasoning = $"Roku only supports HEVC in MP4/M4V/MOV containers, not '{media.Container}'.",
                Severity = RuleSeverity.Require,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateWebBrowser(MediaModel media)
    {
        bool isMkv = media.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase);
        bool isAvi = media.Container.Equals("avi", StringComparison.OrdinalIgnoreCase);
        bool isWmv = media.Container.Equals("wmv", StringComparison.OrdinalIgnoreCase);
        bool isFlv = media.Container.Equals("flv", StringComparison.OrdinalIgnoreCase);

        if (isMkv || isAvi || isWmv || isFlv)
        {
            return new RuleResult
            {
                RuleName = "Web:UnsupportedContainer",
                AllowDirectPlay = false,
                AllowDirectStream = true,
                Reasoning = $"Web browsers cannot natively play '{media.Container}' containers. Remux required.",
                Severity = RuleSeverity.Require,
            };
        }

        bool isHevc = media.VideoCodec.Equals("hevc", StringComparison.OrdinalIgnoreCase)
                      || media.VideoCodec.Equals("h265", StringComparison.OrdinalIgnoreCase);

        if (isHevc)
        {
            return new RuleResult
            {
                RuleName = "Web:HevcLimited",
                AllowDirectPlay = false,
                AllowTranscoding = true,
                Reasoning = "Most web browsers cannot decode HEVC. Transcode to H.264 required.",
                Severity = RuleSeverity.Recommend,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateSwiftfin(MediaModel media)
    {
        bool isWebm = media.Container.Equals("webm", StringComparison.OrdinalIgnoreCase);
        bool isAvi = media.Container.Equals("avi", StringComparison.OrdinalIgnoreCase);

        if (isWebm || isAvi)
        {
            return new RuleResult
            {
                RuleName = "Swiftfin:UnsupportedContainer",
                AllowDirectPlay = false,
                AllowDirectStream = true,
                Reasoning = $"Swiftfin has limited support for '{media.Container}'. Remux recommended.",
                Severity = RuleSeverity.Recommend,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateXbox(MediaModel media)
    {
        bool isMkv = media.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase);
        bool isWebm = media.Container.Equals("webm", StringComparison.OrdinalIgnoreCase);

        if (isMkv || isWebm)
        {
            return new RuleResult
            {
                RuleName = "Xbox:LimitedContainerSupport",
                AllowDirectPlay = false,
                AllowDirectStream = true,
                Reasoning = $"Xbox has limited '{media.Container}' support. Remux to MP4 recommended.",
                Severity = RuleSeverity.Recommend,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateDlna(MediaModel media)
    {
        bool isMkv = media.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase);
        bool isWebm = media.Container.Equals("webm", StringComparison.OrdinalIgnoreCase);

        if (isMkv || isWebm)
        {
            return new RuleResult
            {
                RuleName = "DLNA:IncompatibleContainer",
                AllowDirectPlay = false,
                AllowDirectStream = true,
                Reasoning = $"Most DLNA renderers cannot play '{media.Container}'. Remux to MPEG-TS/MP4.",
                Severity = RuleSeverity.Require,
            };
        }

        return null;
    }

    private static bool IsHevcFriendlyContainer(string container)
    {
        return container.Equals("mp4", StringComparison.OrdinalIgnoreCase)
               || container.Equals("m4v", StringComparison.OrdinalIgnoreCase)
               || container.Equals("mov", StringComparison.OrdinalIgnoreCase);
    }
}
