using System;
using TuneCast.Models;

namespace TuneCast.Decision.Rules;

/// <summary>
/// Detects bit depth incompatibilities.
/// H.264 Hi10P (10-bit H.264) is almost universally unsupported in hardware.
/// 12-bit content has very limited client support.
/// </summary>
public class BitDepthCompatibilityRule : IPlaybackRule
{
    /// <inheritdoc />
    public string Name => "BitDepthCompatibility";

    /// <inheritdoc />
    public RuleResult? Evaluate(ClientModel client, MediaModel media)
    {
        if (!media.VideoBitDepth.HasValue || media.VideoBitDepth.Value <= 8)
        {
            return null;
        }

        bool isH264 = media.VideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase)
                      || media.VideoCodec.Equals("avc", StringComparison.OrdinalIgnoreCase);

        if (isH264 && media.VideoBitDepth.Value >= 10)
        {
            return new RuleResult
            {
                RuleName = "BitDepth:H264Hi10P",
                AllowDirectPlay = false,
                AllowTranscoding = true,
                Reasoning = "H.264 Hi10P (10-bit) has no hardware decoder support on any mainstream client. Transcode required.",
                Severity = RuleSeverity.Require,
            };
        }

        if (media.VideoBitDepth.Value >= 12)
        {
            return client.ClientType switch
            {
                ClientType.Desktop or ClientType.Kodi => null,
                _ => new RuleResult
                {
                    RuleName = "BitDepth:12Bit",
                    AllowDirectPlay = false,
                    AllowTranscoding = true,
                    Reasoning = "12-bit video has very limited client support. Transcode recommended.",
                    Severity = RuleSeverity.Recommend,
                },
            };
        }

        return null;
    }
}
