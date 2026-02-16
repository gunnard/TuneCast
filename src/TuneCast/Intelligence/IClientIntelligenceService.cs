using TuneCast.Models;
using MediaBrowser.Controller.Session;

namespace TuneCast.Intelligence;

/// <summary>
/// Resolves and maintains normalized client models from Jellyfin session data.
/// </summary>
public interface IClientIntelligenceService
{
    /// <summary>
    /// Resolves a <see cref="ClientModel"/> from a Jellyfin session.
    /// Uses a resolver pipeline: UserAgent → DeviceProfile → HistoricalBehavior.
    /// Results are cached per device.
    /// </summary>
    /// <param name="session">The Jellyfin session info.</param>
    /// <returns>A normalized client model.</returns>
    ClientModel ResolveClient(SessionInfo session);

    /// <summary>
    /// Retrieves a cached client model by device identifier without re-resolving.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <returns>The cached client model, or null if not found.</returns>
    ClientModel? GetCachedClient(string deviceId);

    /// <summary>
    /// Invalidates the cached client model for a device.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    void InvalidateCache(string deviceId);
}
