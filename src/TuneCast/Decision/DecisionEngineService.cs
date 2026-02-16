using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TuneCast.Configuration;
using TuneCast.Decision.Rules;
using TuneCast.Models;
using Microsoft.Extensions.Logging;

namespace TuneCast.Decision;

/// <summary>
/// Core decision engine — computes playback policy from client + media models.
/// Deterministic, stateless, and independently testable.
/// </summary>
public class DecisionEngineService : IDecisionEngineService
{
    private const double HighConfidenceThreshold = 0.7;
    private const double LowConfidenceThreshold = 0.4;

    private readonly ILogger<DecisionEngineService> _logger;
    private readonly Func<PluginConfiguration> _configProvider;
    private readonly IReadOnlyList<IPlaybackRule> _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionEngineService"/> class.
    /// Used by Jellyfin DI — pulls config from the live plugin instance.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public DecisionEngineService(ILogger<DecisionEngineService> logger)
        : this(logger, () => Plugin.Instance?.Configuration ?? new PluginConfiguration(), DefaultRules())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionEngineService"/> class.
    /// Used by tests — accepts explicit config and rules for deterministic behavior.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="configProvider">Function that supplies the current configuration.</param>
    /// <param name="rules">Static rules to evaluate. Pass null for defaults.</param>
    internal DecisionEngineService(
        ILogger<DecisionEngineService> logger,
        Func<PluginConfiguration> configProvider,
        IReadOnlyList<IPlaybackRule>? rules = null)
    {
        _logger = logger;
        _configProvider = configProvider;
        _rules = rules ?? DefaultRules();
    }

    private static IReadOnlyList<IPlaybackRule> DefaultRules() => new IPlaybackRule[]
    {
        new ContainerCodecCompatibilityRule(),
        new BitDepthCompatibilityRule(),
        new AudioPassthroughRule(),
        new HdrCompatibilityRule(),
        new BitrateCapRule(),
    };

    /// <inheritdoc />
    public PlaybackPolicy ComputePolicy(ClientModel client, MediaModel media)
    {
        var config = _configProvider();

        if (config.ConservativeMode && !config.EnableDynamicProfiles)
        {
            return PlaybackPolicy.Default();
        }

        var reasoning = new StringBuilder();
        var policy = new PlaybackPolicy();
        double overallConfidence = 0.5;

        // Phase 2: Static rules fire first — deterministic, high-priority
        var ruleResults = EvaluateStaticRules(client, media, reasoning);
        ApplyRuleResults(ruleResults, policy, ref overallConfidence);

        // Phase 1: Confidence-based evaluation fills remaining gaps
        EvaluateVideoCodecSupport(client, media, policy, reasoning, ref overallConfidence);
        EvaluateAudioCodecSupport(client, media, policy, reasoning, ref overallConfidence);
        EvaluateContainerSupport(client, media, policy, reasoning, ref overallConfidence);
        EvaluateBitrate(client, media, config, policy, reasoning);
        EvaluateTranscodeCost(media, policy, reasoning);

        policy.Confidence = Math.Clamp(overallConfidence, 0.0, 1.0);
        policy.Reasoning = reasoning.ToString();

        if (policy.Confidence < LowConfidenceThreshold)
        {
            _logger.LogDebug(
                "Low confidence ({Confidence:F2}) for {DeviceId} playing {MediaSourceId} — deferring to Jellyfin defaults",
                policy.Confidence,
                client.DeviceId,
                media.MediaSourceId);

            return PlaybackPolicy.Default();
        }

        _logger.LogDebug(
            "Policy for {DeviceId} → {MediaSourceId}: DirectPlay={DP}, DirectStream={DS}, Transcode={TC}, Confidence={C:F2}",
            client.DeviceId,
            media.MediaSourceId,
            policy.AllowDirectPlay,
            policy.AllowDirectStream,
            policy.AllowTranscoding,
            policy.Confidence);

        return policy;
    }

    private List<RuleResult> EvaluateStaticRules(ClientModel client, MediaModel media, StringBuilder reasoning)
    {
        var results = new List<RuleResult>();

        foreach (var rule in _rules)
        {
            try
            {
                var result = rule.Evaluate(client, media);
                if (result is not null)
                {
                    results.Add(result);
                    reasoning.AppendLine($"[Rule:{result.RuleName}] {result.Reasoning}");

                    _logger.LogDebug(
                        "Rule {RuleName} fired for {DeviceId} → {MediaSourceId}: {Reasoning}",
                        result.RuleName,
                        client.DeviceId,
                        media.MediaSourceId,
                        result.Reasoning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rule {RuleName} threw an exception — skipping", rule.Name);
            }
        }

        return results;
    }

    private static void ApplyRuleResults(List<RuleResult> results, PlaybackPolicy policy, ref double overallConfidence)
    {
        if (results.Count == 0)
        {
            return;
        }

        var ordered = results.OrderByDescending(r => r.Severity);

        RuleSeverity? directPlaySeverity = null;
        RuleSeverity? directStreamSeverity = null;
        RuleSeverity? transcodeSeverity = null;

        foreach (var result in ordered)
        {
            if (result.AllowDirectPlay.HasValue && (!directPlaySeverity.HasValue || result.Severity >= directPlaySeverity.Value))
            {
                policy.AllowDirectPlay = result.AllowDirectPlay.Value;
                directPlaySeverity = result.Severity;
            }

            if (result.AllowDirectStream.HasValue && (!directStreamSeverity.HasValue || result.Severity >= directStreamSeverity.Value))
            {
                policy.AllowDirectStream = result.AllowDirectStream.Value;
                directStreamSeverity = result.Severity;
            }

            if (result.AllowTranscoding.HasValue && (!transcodeSeverity.HasValue || result.Severity >= transcodeSeverity.Value))
            {
                policy.AllowTranscoding = result.AllowTranscoding.Value;
                transcodeSeverity = result.Severity;
            }

            if (result.BitrateCap.HasValue)
            {
                policy.BitrateCap = policy.BitrateCap.HasValue
                    ? Math.Min(policy.BitrateCap.Value, result.BitrateCap.Value)
                    : result.BitrateCap.Value;
            }
        }

        int requireCount = results.Count(r => r.Severity == RuleSeverity.Require);
        int recommendCount = results.Count(r => r.Severity == RuleSeverity.Recommend);

        overallConfidence += requireCount * 0.15;
        overallConfidence += recommendCount * 0.1;
    }

    private static void EvaluateVideoCodecSupport(
        ClientModel client,
        MediaModel media,
        PlaybackPolicy policy,
        StringBuilder reasoning,
        ref double confidence)
    {
        if (string.IsNullOrEmpty(media.VideoCodec))
        {
            reasoning.AppendLine("No video codec info available — allowing all methods.");
            return;
        }

        if (!client.CodecConfidence.TryGetValue(media.VideoCodec, out double codecConfidence))
        {
            reasoning.AppendLine($"Video codec '{media.VideoCodec}' — no confidence data, deferring.");
            return;
        }

        if (codecConfidence >= HighConfidenceThreshold)
        {
            policy.AllowDirectPlay = true;
            reasoning.AppendLine($"Video codec '{media.VideoCodec}' confidence {codecConfidence:F2} — favoring direct play.");
            confidence += 0.2;
        }
        else if (codecConfidence >= LowConfidenceThreshold)
        {
            policy.AllowDirectPlay = true;
            policy.AllowDirectStream = true;
            reasoning.AppendLine($"Video codec '{media.VideoCodec}' confidence {codecConfidence:F2} — allowing direct play with fallback.");
        }
        else
        {
            policy.AllowDirectPlay = false;
            policy.AllowTranscoding = true;
            reasoning.AppendLine($"Video codec '{media.VideoCodec}' confidence {codecConfidence:F2} — codec likely unsupported, allowing transcode.");
            confidence -= 0.1;
        }
    }

    private static void EvaluateAudioCodecSupport(
        ClientModel client,
        MediaModel media,
        PlaybackPolicy policy,
        StringBuilder reasoning,
        ref double confidence)
    {
        if (string.IsNullOrEmpty(media.AudioCodec))
        {
            return;
        }

        if (!client.CodecConfidence.TryGetValue(media.AudioCodec, out double audioConfidence))
        {
            reasoning.AppendLine($"Audio codec '{media.AudioCodec}' — no confidence data, deferring.");
            return;
        }

        if (audioConfidence >= HighConfidenceThreshold)
        {
            reasoning.AppendLine($"Audio codec '{media.AudioCodec}' confidence {audioConfidence:F2} — compatible.");
            confidence += 0.05;
        }
        else if (audioConfidence >= LowConfidenceThreshold)
        {
            reasoning.AppendLine($"Audio codec '{media.AudioCodec}' confidence {audioConfidence:F2} — may need audio transcode.");
        }
        else
        {
            policy.AllowTranscoding = true;
            reasoning.AppendLine($"Audio codec '{media.AudioCodec}' confidence {audioConfidence:F2} — audio transcode likely required.");

            if (policy.AllowDirectPlay && media.TranscodeCostEstimate <= TranscodeCost.Remux)
            {
                reasoning.AppendLine("Audio-only transcode is cheap — direct stream with audio transcode preferred over full video transcode.");
                policy.AllowDirectStream = true;
            }

            confidence -= 0.05;
        }
    }

    private static void EvaluateContainerSupport(
        ClientModel client,
        MediaModel media,
        PlaybackPolicy policy,
        StringBuilder reasoning,
        ref double confidence)
    {
        if (string.IsNullOrEmpty(media.Container))
        {
            return;
        }

        if (!client.ContainerConfidence.TryGetValue(media.Container, out double containerConfidence))
        {
            reasoning.AppendLine($"Container '{media.Container}' — no confidence data, deferring.");
            return;
        }

        if (containerConfidence >= HighConfidenceThreshold)
        {
            reasoning.AppendLine($"Container '{media.Container}' confidence {containerConfidence:F2} — no remux needed.");
            confidence += 0.1;
        }
        else if (containerConfidence >= LowConfidenceThreshold)
        {
            policy.AllowDirectStream = true;
            reasoning.AppendLine($"Container '{media.Container}' confidence {containerConfidence:F2} — may need remux.");
        }
        else
        {
            if (policy.AllowDirectPlay)
            {
                policy.AllowDirectPlay = false;
                policy.AllowDirectStream = true;
                reasoning.AppendLine($"Container '{media.Container}' confidence {containerConfidence:F2} — forcing direct stream/remux over direct play.");
            }
        }
    }

    private static void EvaluateBitrate(
        ClientModel client,
        MediaModel media,
        PluginConfiguration config,
        PlaybackPolicy policy,
        StringBuilder reasoning)
    {
        int? effectiveCap = config.GlobalMaxBitrateOverride ?? client.MaxBitrate;

        if (effectiveCap.HasValue && media.Bitrate.HasValue && media.Bitrate.Value > effectiveCap.Value)
        {
            policy.BitrateCap = effectiveCap.Value;
            policy.AllowTranscoding = true;
            reasoning.AppendLine($"Media bitrate {media.Bitrate.Value / 1_000_000.0:F1} Mbps exceeds cap {effectiveCap.Value / 1_000_000.0:F1} Mbps — transcode may be needed.");
        }
    }

    private static void EvaluateTranscodeCost(
        MediaModel media,
        PlaybackPolicy policy,
        StringBuilder reasoning)
    {
        switch (media.TranscodeCostEstimate)
        {
            case TranscodeCost.Extreme:
                reasoning.AppendLine("Transcode cost: EXTREME — strongly prefer direct play if possible.");
                if (!policy.AllowDirectPlay)
                {
                    reasoning.AppendLine("WARNING: Direct play disallowed but transcode will be very expensive.");
                }

                break;

            case TranscodeCost.High:
                reasoning.AppendLine("Transcode cost: HIGH — prefer direct play/stream.");
                break;

            case TranscodeCost.Medium:
                reasoning.AppendLine("Transcode cost: MEDIUM — transcode is manageable but direct play still preferred.");
                break;

            case TranscodeCost.Low:
                reasoning.AppendLine("Transcode cost: LOW — lightweight transcode, no significant server impact.");
                break;

            case TranscodeCost.Remux:
                reasoning.AppendLine("Transcode cost: REMUX only — container remux is cheap, direct stream is fine.");
                policy.AllowDirectStream = true;
                break;
        }
    }
}
