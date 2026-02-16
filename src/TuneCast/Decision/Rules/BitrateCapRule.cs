using TuneCast.Models;

namespace TuneCast.Decision.Rules;

/// <summary>
/// Applies bitrate caps for clients known to have bandwidth constraints.
/// Mobile clients on cellular, Roku over WiFi, and DLNA renderers often
/// cannot handle high bitrate streams.
/// </summary>
public class BitrateCapRule : IPlaybackRule
{
    private const int MobileCap = 8_000_000;       // 8 Mbps
    private const int RokuDefaultCap = 20_000_000; // 20 Mbps
    private const int DlnaCap = 15_000_000;        // 15 Mbps

    /// <inheritdoc />
    public string Name => "BitrateCap";

    /// <inheritdoc />
    public RuleResult? Evaluate(ClientModel client, MediaModel media)
    {
        if (!media.Bitrate.HasValue)
        {
            return null;
        }

        int? cap = GetDefaultCap(client.ClientType);

        if (!cap.HasValue || media.Bitrate.Value <= cap.Value)
        {
            return null;
        }

        return new RuleResult
        {
            RuleName = $"BitrateCap:{client.ClientType}",
            AllowTranscoding = true,
            BitrateCap = cap.Value,
            Reasoning = $"Media bitrate {media.Bitrate.Value / 1_000_000.0:F1} Mbps exceeds " +
                        $"{client.ClientType} default cap of {cap.Value / 1_000_000.0:F1} Mbps.",
            Severity = RuleSeverity.Suggest,
        };
    }

    private static int? GetDefaultCap(ClientType clientType)
    {
        return clientType switch
        {
            ClientType.AndroidMobile => MobileCap,
            ClientType.SwiftfinIos => MobileCap,
            ClientType.Roku => RokuDefaultCap,
            ClientType.Dlna => DlnaCap,
            _ => null,
        };
    }
}
