using MediaBrowser.Model.Plugins;

namespace TuneCast.Configuration;

/// <summary>
/// Plugin configuration — exposed via Jellyfin dashboard.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether dynamic profile shaping is enabled.
    /// When false, the plugin only observes (telemetry mode).
    /// </summary>
    public bool EnableDynamicProfiles { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the learning/adaptation layer is active.
    /// </summary>
    public bool EnableLearning { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether conservative mode is active.
    /// Conservative mode defers to Jellyfin defaults on any uncertain decision.
    /// </summary>
    public bool ConservativeMode { get; set; } = true;

    /// <summary>
    /// Gets or sets the weight applied to server load when biasing decisions.
    /// Range: 0.0 (ignore load) to 1.0 (heavily weight load).
    /// </summary>
    public double ServerLoadBiasWeight { get; set; } = 0.3;

    /// <summary>
    /// Gets or sets the global max bitrate override in bits/sec.
    /// Null means defer to Jellyfin/client defaults.
    /// </summary>
    public int? GlobalMaxBitrateOverride { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether verbose telemetry logging is enabled.
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of days to retain telemetry data before pruning.
    /// </summary>
    public int TelemetryRetentionDays { get; set; } = 90;
}
