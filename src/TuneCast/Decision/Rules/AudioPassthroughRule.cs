using System;
using TuneCast.Models;

namespace TuneCast.Decision.Rules;

/// <summary>
/// Detects audio codecs that require transcoding on specific client types.
/// Lossless audio (TrueHD, DTS-HD MA) can't be decoded by most clients natively
/// and require either passthrough to a receiver or audio transcoding.
/// </summary>
public class AudioPassthroughRule : IPlaybackRule
{
    /// <inheritdoc />
    public string Name => "AudioPassthrough";

    /// <inheritdoc />
    public RuleResult? Evaluate(ClientModel client, MediaModel media)
    {
        if (string.IsNullOrEmpty(media.AudioCodec))
        {
            return null;
        }

        bool isTrueHd = media.AudioCodec.Equals("truehd", StringComparison.OrdinalIgnoreCase);
        bool isDtsHdMa = media.AudioCodec.Equals("dts-hd ma", StringComparison.OrdinalIgnoreCase);
        bool isDtsHdHra = media.AudioCodec.Equals("dts-hd hra", StringComparison.OrdinalIgnoreCase);
        bool isDts = media.AudioCodec.Equals("dts", StringComparison.OrdinalIgnoreCase);
        bool isLossless = isTrueHd || isDtsHdMa || isDtsHdHra;

        return client.ClientType switch
        {
            ClientType.WebBrowser => EvaluateWeb(media, isTrueHd, isDtsHdMa, isDtsHdHra, isDts),
            ClientType.AndroidMobile => EvaluateMobile(media, isLossless, isDts),
            ClientType.Roku => EvaluateRoku(media, isTrueHd, isDtsHdMa),
            ClientType.SwiftfinIos => EvaluateSwiftfinIos(media, isDts, isDtsHdMa, isDtsHdHra),
            _ => EvaluateGenericLossless(media, isLossless, client.ClientType),
        };
    }

    private static RuleResult? EvaluateWeb(MediaModel media, bool isTrueHd, bool isDtsHdMa, bool isDtsHdHra, bool isDts)
    {
        if (isTrueHd || isDtsHdMa || isDtsHdHra || isDts)
        {
            return new RuleResult
            {
                RuleName = "Audio:Web:NoLosslessOrDts",
                AllowTranscoding = true,
                Reasoning = $"Web browsers cannot decode '{media.AudioCodec}'. Audio transcode to AAC/Opus required.",
                Severity = RuleSeverity.Require,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateMobile(MediaModel media, bool isLossless, bool isDts)
    {
        if (isLossless || isDts)
        {
            return new RuleResult
            {
                RuleName = "Audio:Mobile:NoPassthrough",
                AllowTranscoding = true,
                Reasoning = $"Mobile devices cannot passthrough '{media.AudioCodec}'. Audio transcode required.",
                Severity = RuleSeverity.Require,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateRoku(MediaModel media, bool isTrueHd, bool isDtsHdMa)
    {
        if (isTrueHd || isDtsHdMa)
        {
            return new RuleResult
            {
                RuleName = "Audio:Roku:NoLossless",
                AllowTranscoding = true,
                Reasoning = $"Roku cannot decode or passthrough '{media.AudioCodec}'. Audio transcode required.",
                Severity = RuleSeverity.Require,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateSwiftfinIos(MediaModel media, bool isDts, bool isDtsHdMa, bool isDtsHdHra)
    {
        if (isDts || isDtsHdMa || isDtsHdHra)
        {
            return new RuleResult
            {
                RuleName = "Audio:Swiftfin:NoDts",
                AllowTranscoding = true,
                Reasoning = $"iOS/Apple has no native DTS support. Audio transcode of '{media.AudioCodec}' required.",
                Severity = RuleSeverity.Require,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateGenericLossless(MediaModel media, bool isLossless, ClientType clientType)
    {
        if (!isLossless)
        {
            return null;
        }

        if (clientType == ClientType.Desktop || clientType == ClientType.Kodi)
        {
            return null;
        }

        return new RuleResult
        {
            RuleName = "Audio:Generic:LosslessUnsupported",
            AllowTranscoding = true,
            Reasoning = $"Lossless audio '{media.AudioCodec}' may not be supported. Audio transcode available as fallback.",
            Severity = RuleSeverity.Suggest,
        };
    }
}
