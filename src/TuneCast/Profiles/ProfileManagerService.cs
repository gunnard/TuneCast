using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using TuneCast.Configuration;
using TuneCast.Models;
using TuneCast.Storage;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace TuneCast.Profiles;

/// <summary>
/// Phase 3 implementation: builds shaped DeviceProfiles from policy decisions
/// and persists them via IDeviceManager so Jellyfin's native playback resolution
/// respects our intelligence.
/// </summary>
public class ProfileManagerService : IProfileManagerService
{
    private readonly ILogger<ProfileManagerService> _logger;
    private readonly IDeviceManager _deviceManager;
    private readonly IPluginDataStore _store;
    private readonly ISessionManager _sessionManager;
    private readonly ConcurrentDictionary<string, DeviceProfile> _appliedProfiles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileManagerService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="deviceManager">Jellyfin device manager for persisting capabilities.</param>
    /// <param name="store">Plugin data store for recording interventions.</param>
    /// <param name="sessionManager">Session manager for sending client notifications.</param>
    public ProfileManagerService(
        ILogger<ProfileManagerService> logger,
        IDeviceManager deviceManager,
        IPluginDataStore store,
        ISessionManager sessionManager)
    {
        _logger = logger;
        _deviceManager = deviceManager;
        _store = store;
        _sessionManager = sessionManager;
    }

    /// <inheritdoc />
    public void ApplyPolicy(ClientModel client, MediaModel media, PlaybackPolicy policy)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        if (!config.EnableDynamicProfiles)
        {
            LogDryRun(client, media, policy);
            return;
        }

        if (policy.IsDefault)
        {
            _logger.LogDebug(
                "Policy is default pass-through for {DeviceId} — no profile shaping needed",
                client.DeviceId);
            return;
        }

        try
        {
            var shapedProfile = ProfileBuilder.Build(client, policy);
            if (shapedProfile is null)
            {
                return;
            }

            SaveDeviceProfile(client.DeviceId, shapedProfile);

            _logger.LogInformation(
                "[ACTIVE] Applied shaped profile for {DeviceId} → {MediaSourceId}: " +
                "DirectPlay={DP}, DirectStream={DS}, Transcode={TC}, BitrateCap={Bitrate}, " +
                "DirectPlayProfiles={DPCount}, TranscodingProfiles={TCCount}",
                client.DeviceId,
                media.MediaSourceId,
                policy.AllowDirectPlay,
                policy.AllowDirectStream,
                policy.AllowTranscoding,
                policy.BitrateCap,
                shapedProfile.DirectPlayProfiles?.Length ?? 0,
                shapedProfile.TranscodingProfiles?.Length ?? 0);

            RecordInterventionEvent(client, media, policy, isActive: true);
            SendPlaybackToast(client, policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply shaped profile for {DeviceId}", client.DeviceId);
        }
    }

    /// <inheritdoc />
    public DeviceProfile? GetAppliedProfile(string deviceId)
    {
        _appliedProfiles.TryGetValue(deviceId, out var profile);
        return profile;
    }

    /// <inheritdoc />
    public void ClearAppliedProfile(string deviceId)
    {
        _appliedProfiles.TryRemove(deviceId, out _);

        _logger.LogDebug("Cleared applied profile for {DeviceId}", deviceId);
    }

    private void SaveDeviceProfile(string deviceId, DeviceProfile profile)
    {
        _appliedProfiles[deviceId] = profile;

        try
        {
            var capabilities = _deviceManager.GetCapabilities(deviceId);
            if (capabilities is not null)
            {
                capabilities.DeviceProfile = profile;
                _deviceManager.SaveCapabilities(deviceId, capabilities);

                _logger.LogDebug(
                    "Persisted shaped profile to IDeviceManager for {DeviceId}",
                    deviceId);
            }
            else
            {
                _logger.LogDebug(
                    "No existing capabilities for {DeviceId} — profile cached in-memory only",
                    deviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not persist profile via IDeviceManager for {DeviceId} — using in-memory cache only",
                deviceId);
        }
    }

    private void LogDryRun(ClientModel client, MediaModel media, PlaybackPolicy policy)
    {
        var shapedProfile = ProfileBuilder.Build(client, policy);
        int dpCount = shapedProfile?.DirectPlayProfiles?.Length ?? 0;
        int tcCount = shapedProfile?.TranscodingProfiles?.Length ?? 0;

        _logger.LogDebug(
            "[DRY RUN] Would apply policy for {DeviceId} → {MediaSourceId}: " +
            "DirectPlay={DP}, DirectStream={DS}, Transcode={TC}, BitrateCap={Bitrate}, " +
            "Confidence={Confidence:F2}, DirectPlayProfiles={DPCount}, TranscodingProfiles={TCCount}. " +
            "Reason: {Reasoning}",
            client.DeviceId,
            media.MediaSourceId,
            policy.AllowDirectPlay,
            policy.AllowDirectStream,
            policy.AllowTranscoding,
            policy.BitrateCap,
            policy.Confidence,
            dpCount,
            tcCount,
            policy.Reasoning);

        if (!policy.IsDefault)
        {
            RecordInterventionEvent(client, media, policy, isActive: false);
        }
    }

    private void RecordInterventionEvent(ClientModel client, MediaModel media, PlaybackPolicy policy, bool isActive)
    {
        try
        {
            _store.RecordIntervention(new InterventionRecord
            {
                Timestamp = DateTime.UtcNow,
                DeviceId = client.DeviceId,
                DeviceName = client.DeviceName,
                ClientName = client.ClientName,
                MediaSourceId = media.MediaSourceId,
                IsActive = isActive,
                AllowDirectPlay = policy.AllowDirectPlay,
                AllowDirectStream = policy.AllowDirectStream,
                AllowTranscoding = policy.AllowTranscoding,
                BitrateCap = policy.BitrateCap,
                Confidence = policy.Confidence,
                Reasoning = policy.Reasoning
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record intervention for {DeviceId}", client.DeviceId);
        }
    }

    private void SendPlaybackToast(ClientModel client, PlaybackPolicy policy)
    {
        try
        {
            var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
            if (!config.EnableVerboseLogging)
            {
                return;
            }

            var session = FindSessionByDeviceId(client.DeviceId);
            if (session is null)
            {
                return;
            }

            var method = policy.AllowDirectPlay ? "Direct Play" : (policy.AllowDirectStream ? "Direct Stream" : "Transcode");
            var header = "TuneCast";
            var text = $"Optimized for {method} (confidence: {policy.Confidence:P0})";

            var command = new GeneralCommand
            {
                Name = GeneralCommandType.DisplayMessage,
            };
            command.Arguments["Header"] = header;
            command.Arguments["Text"] = text;
            command.Arguments["TimeoutMs"] = "5000";

            _sessionManager.SendGeneralCommand(
                controllingSessionId: null,
                sessionId: session.Id,
                command: command,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not send playback toast to {DeviceId}", client.DeviceId);
        }
    }

    private SessionInfo? FindSessionByDeviceId(string deviceId)
    {
        foreach (var session in _sessionManager.Sessions)
        {
            if (string.Equals(session.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return session;
            }
        }

        return null;
    }
}
