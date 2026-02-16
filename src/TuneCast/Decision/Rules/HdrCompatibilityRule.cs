using System;
using TuneCast.Models;

namespace TuneCast.Decision.Rules;

/// <summary>
/// Detects HDR/Dolby Vision content that will require tone-mapping on clients
/// that don't support HDR output. Tone-mapping is CPU-expensive and should
/// be flagged clearly in policy decisions.
/// </summary>
public class HdrCompatibilityRule : IPlaybackRule
{
    /// <inheritdoc />
    public string Name => "HdrCompatibility";

    /// <inheritdoc />
    public RuleResult? Evaluate(ClientModel client, MediaModel media)
    {
        if (string.IsNullOrEmpty(media.VideoRangeType)
            || media.VideoRangeType.Equals("SDR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        bool isDolbyVision = media.VideoRangeType.Contains("DoVi", StringComparison.OrdinalIgnoreCase)
                             || media.VideoRangeType.Contains("DolbyVision", StringComparison.OrdinalIgnoreCase)
                             || media.VideoRangeType.Contains("Dolby Vision", StringComparison.OrdinalIgnoreCase);

        return client.ClientType switch
        {
            ClientType.WebBrowser => EvaluateWeb(media, isDolbyVision),
            ClientType.Roku => EvaluateRoku(media, isDolbyVision),
            ClientType.AndroidMobile => EvaluateMobile(media, isDolbyVision),
            ClientType.Xbox => EvaluateXbox(media, isDolbyVision),
            ClientType.Desktop or ClientType.Kodi => null,
            ClientType.SwiftfinTvos => EvaluateAppleTv(media, isDolbyVision),
            ClientType.SwiftfinIos => EvaluateSwiftfinIos(media, isDolbyVision),
            _ => EvaluateGenericHdr(media, isDolbyVision),
        };
    }

    private static RuleResult EvaluateWeb(MediaModel media, bool isDolbyVision)
    {
        return new RuleResult
        {
            RuleName = isDolbyVision ? "HDR:Web:DolbyVision" : "HDR:Web:ToneMapRequired",
            AllowDirectPlay = false,
            AllowTranscoding = true,
            Reasoning = isDolbyVision
                ? "Web browsers cannot play Dolby Vision. Transcode with tone-mapping to SDR required."
                : $"Web browsers cannot display {media.VideoRangeType}. Transcode with tone-mapping to SDR required.",
            Severity = RuleSeverity.Require,
        };
    }

    private static RuleResult? EvaluateRoku(MediaModel media, bool isDolbyVision)
    {
        if (isDolbyVision)
        {
            return new RuleResult
            {
                RuleName = "HDR:Roku:DolbyVision",
                AllowDirectPlay = false,
                AllowTranscoding = true,
                Reasoning = "Roku has limited Dolby Vision support. Transcode with tone-mapping likely required.",
                Severity = RuleSeverity.Recommend,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateMobile(MediaModel media, bool isDolbyVision)
    {
        return new RuleResult
        {
            RuleName = isDolbyVision ? "HDR:Mobile:DolbyVision" : "HDR:Mobile:ToneMapRequired",
            AllowDirectPlay = false,
            AllowTranscoding = true,
            Reasoning = $"Mobile devices typically cannot display {media.VideoRangeType}. Tone-mapping transcode required.",
            Severity = RuleSeverity.Recommend,
        };
    }

    private static RuleResult? EvaluateXbox(MediaModel media, bool isDolbyVision)
    {
        if (isDolbyVision)
        {
            return new RuleResult
            {
                RuleName = "HDR:Xbox:DolbyVision",
                AllowDirectPlay = false,
                AllowTranscoding = true,
                Reasoning = "Xbox Dolby Vision support is limited. Tone-mapping transcode recommended.",
                Severity = RuleSeverity.Recommend,
            };
        }

        return null;
    }

    private static RuleResult? EvaluateAppleTv(MediaModel media, bool isDolbyVision)
    {
        if (isDolbyVision)
        {
            return null;
        }

        return null;
    }

    private static RuleResult? EvaluateSwiftfinIos(MediaModel media, bool isDolbyVision)
    {
        return new RuleResult
        {
            RuleName = isDolbyVision ? "HDR:iOS:DolbyVision" : "HDR:iOS:LimitedHdr",
            AllowDirectPlay = false,
            AllowTranscoding = true,
            Reasoning = $"iPhone displays have limited {media.VideoRangeType} support. Tone-mapping transcode recommended.",
            Severity = RuleSeverity.Suggest,
        };
    }

    private static RuleResult? EvaluateGenericHdr(MediaModel media, bool isDolbyVision)
    {
        if (!isDolbyVision)
        {
            return null;
        }

        return new RuleResult
        {
            RuleName = "HDR:Generic:DolbyVision",
            AllowDirectPlay = false,
            AllowTranscoding = true,
            Reasoning = "Dolby Vision support is uncommon. Tone-mapping transcode may be required.",
            Severity = RuleSeverity.Suggest,
        };
    }
}
