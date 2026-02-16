namespace TuneCast.Decision.Rules;

/// <summary>
/// The outcome of a static rule evaluation.
/// Null return from a rule means it doesn't apply.
/// </summary>
public class RuleResult
{
    /// <summary>
    /// Gets or sets the name of the rule that produced this result.
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether direct play should be allowed.
    /// Null means the rule has no opinion on this dimension.
    /// </summary>
    public bool? AllowDirectPlay { get; set; }

    /// <summary>
    /// Gets or sets whether direct stream (remux) should be allowed.
    /// Null means the rule has no opinion.
    /// </summary>
    public bool? AllowDirectStream { get; set; }

    /// <summary>
    /// Gets or sets whether transcoding should be allowed.
    /// Null means the rule has no opinion.
    /// </summary>
    public bool? AllowTranscoding { get; set; }

    /// <summary>
    /// Gets or sets a recommended bitrate cap in bits/sec.
    /// Null means no cap recommendation.
    /// </summary>
    public int? BitrateCap { get; set; }

    /// <summary>
    /// Gets or sets the human-readable reasoning for this rule's decision.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the severity of this rule's recommendation.
    /// Higher severity rules override lower ones on conflicting dimensions.
    /// </summary>
    public RuleSeverity Severity { get; set; } = RuleSeverity.Suggest;
}

/// <summary>
/// How strongly the rule feels about its recommendation.
/// </summary>
public enum RuleSeverity
{
    /// <summary>Soft suggestion — can be overridden by other signals.</summary>
    Suggest = 0,

    /// <summary>Strong recommendation — should be followed unless contradicted by higher severity.</summary>
    Recommend = 1,

    /// <summary>Hard constraint — known incompatibility, ignoring will cause playback failure.</summary>
    Require = 2,
}
