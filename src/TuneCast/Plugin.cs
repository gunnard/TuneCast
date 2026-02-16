using System;
using System.Collections.Generic;
using TuneCast.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace TuneCast;

/// <summary>
/// TuneCast — Adaptive playback policy engine for Jellyfin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Plugin unique identifier.
    /// </summary>
    public static readonly Guid PluginId = Guid.Parse("d4e5f6a7-b8c9-0d1e-2f3a-4b5c6d7e8f90");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "TuneCast";

    /// <inheritdoc />
    public override string Description => "Adaptive playback policy engine — biases Jellyfin toward optimal direct play decisions.";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            },
            new PluginPageInfo
            {
                Name = $"{Name} Dashboard",
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.dashboardPage.html",
                MenuSection = "server",
                MenuIcon = "dashboard",
                DisplayName = "TuneCast Dashboard"
            }
        };
    }
}
