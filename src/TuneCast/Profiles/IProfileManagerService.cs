using TuneCast.Models;
using MediaBrowser.Model.Dlna;

namespace TuneCast.Profiles;

/// <summary>
/// Manages dynamic device profile adjustments.
/// This is the primary lever for safely influencing Jellyfin's playback decisions.
/// Profiles = persuasion, not coercion.
/// </summary>
public interface IProfileManagerService
{
    /// <summary>
    /// Applies a playback policy by building and persisting a shaped DeviceProfile.
    /// When EnableDynamicProfiles is off, logs the intended action as a dry run.
    /// </summary>
    /// <param name="client">The resolved client model.</param>
    /// <param name="media">The resolved media model.</param>
    /// <param name="policy">The computed playback policy.</param>
    void ApplyPolicy(ClientModel client, MediaModel media, PlaybackPolicy policy);

    /// <summary>
    /// Retrieves the most recently applied shaped profile for a device.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <returns>The shaped profile, or null if none has been applied.</returns>
    DeviceProfile? GetAppliedProfile(string deviceId);

    /// <summary>
    /// Clears the applied profile for a device, reverting to Jellyfin defaults.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    void ClearAppliedProfile(string deviceId);
}
