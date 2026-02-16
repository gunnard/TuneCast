using TuneCast.Decision;
using TuneCast.Intelligence;
using TuneCast.Learning;
using TuneCast.Profiles;
using TuneCast.Storage;
using TuneCast.Telemetry;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace TuneCast;

/// <summary>
/// Registers all TuneCast services into the Jellyfin DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPluginDataStore, LiteDbDataStore>();
        serviceCollection.AddSingleton<IClientIntelligenceService, ClientIntelligenceService>();
        serviceCollection.AddSingleton<IMediaIntelligenceService, MediaIntelligenceService>();
        serviceCollection.AddSingleton<IDecisionEngineService, DecisionEngineService>();
        serviceCollection.AddSingleton<IProfileManagerService, ProfileManagerService>();
        serviceCollection.AddSingleton<ITelemetryService, TelemetryService>();
        serviceCollection.AddSingleton<ILearningService, LearningService>();
        serviceCollection.AddHostedService<PlaybackEventHandler>();
    }
}
