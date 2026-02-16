using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TuneCast.Decision;
using TuneCast.Intelligence;
using TuneCast.Learning;
using TuneCast.Models;
using TuneCast.Profiles;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TuneCast.Telemetry;

/// <summary>
/// Hosted service that subscribes to Jellyfin session lifecycle events.
/// This is the main entry point for the entire plugin pipeline:
///   Session event → Resolve client/media → Compute policy → Apply/Log → Record telemetry.
/// </summary>
public class PlaybackEventHandler : IHostedService
{
    private readonly ISessionManager _sessionManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IClientIntelligenceService _clientIntelligence;
    private readonly IMediaIntelligenceService _mediaIntelligence;
    private readonly IDecisionEngineService _decisionEngine;
    private readonly IProfileManagerService _profileManager;
    private readonly ITelemetryService _telemetry;
    private readonly ILearningService _learningService;
    private readonly ILogger<PlaybackEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackEventHandler"/> class.
    /// </summary>
    public PlaybackEventHandler(
        ISessionManager sessionManager,
        ILibraryManager libraryManager,
        IClientIntelligenceService clientIntelligence,
        IMediaIntelligenceService mediaIntelligence,
        IDecisionEngineService decisionEngine,
        IProfileManagerService profileManager,
        ITelemetryService telemetry,
        ILearningService learningService,
        ILogger<PlaybackEventHandler> logger)
    {
        _sessionManager = sessionManager;
        _libraryManager = libraryManager;
        _clientIntelligence = clientIntelligence;
        _mediaIntelligence = mediaIntelligence;
        _decisionEngine = decisionEngine;
        _profileManager = profileManager;
        _telemetry = telemetry;
        _learningService = learningService;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.SessionStarted += OnSessionStarted;
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _sessionManager.SessionEnded += OnSessionEnded;

        _logger.LogInformation("TuneCast playback event handler started — listening for session events");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.SessionStarted -= OnSessionStarted;
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _sessionManager.SessionEnded -= OnSessionEnded;

        _logger.LogInformation("TuneCast playback event handler stopped");
        return Task.CompletedTask;
    }

    private void OnSessionStarted(object? sender, SessionEventArgs e)
    {
        try
        {
            var session = e.SessionInfo;
            _logger.LogDebug(
                "Session started: {Client} v{Version} on {DeviceName} ({DeviceId})",
                session.Client,
                session.ApplicationVersion,
                session.DeviceName,
                session.DeviceId);

            var client = _clientIntelligence.ResolveClient(session);

            var baselineMedia = new MediaModel
            {
                MediaSourceId = "baseline",
                ItemId = "baseline",
            };
            var baselinePolicy = _decisionEngine.ComputePolicy(client, baselineMedia);
            _profileManager.ApplyPolicy(client, baselineMedia, baselinePolicy);

            _logger.LogDebug(
                "Pre-loaded baseline profile for {DeviceId} ({ClientType})",
                client.DeviceId,
                client.ClientType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session start for {DeviceId}", e.SessionInfo?.DeviceId);
        }
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        try
        {
            var session = e.Session;
            if (session is null)
            {
                return;
            }

            var client = _clientIntelligence.ResolveClient(session);

            var nowPlayingItem = session.NowPlayingItem;
            var mediaSourceId = e.MediaSourceId ?? string.Empty;
            var playSessionId = e.PlaySessionId ?? string.Empty;

            MediaModel? media = null;
            if (e.MediaInfo is not null)
            {
                var mediaSources = e.MediaInfo.MediaSources;

                if (mediaSources is not null && mediaSources.Any())
                {
                    var activeSource = mediaSources
                        .FirstOrDefault(s => string.Equals(s.Id, mediaSourceId, StringComparison.OrdinalIgnoreCase))
                        ?? mediaSources.First();

                    media = _mediaIntelligence.ResolveMedia(activeSource, nowPlayingItem?.Id.ToString("N") ?? string.Empty);
                }
            }

            media ??= new MediaModel { MediaSourceId = mediaSourceId, ItemId = nowPlayingItem?.Id.ToString("N") ?? string.Empty };

            var policy = _decisionEngine.ComputePolicy(client, media);

            _profileManager.ApplyPolicy(client, media, policy);

            var playMethod = session.PlayState?.PlayMethod?.ToString() ?? "Unknown";
            var transcodeReasons = session.NowPlayingItem is not null
                ? (session.PlayState?.PlayMethod?.ToString() == "Transcode" ? "TranscodeTriggered" : string.Empty)
                : string.Empty;

            _telemetry.RecordPlaybackStart(
                client,
                media,
                policy,
                playSessionId,
                playMethod,
                transcodeReasons);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback start for {DeviceId}", e.Session?.DeviceId);
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        try
        {
            var playSessionId = e.PlaySessionId ?? string.Empty;
            if (string.IsNullOrEmpty(playSessionId))
            {
                return;
            }

            var outcome = _telemetry.RecordPlaybackStop(
                playSessionId,
                e.PlaybackPositionTicks,
                e.MediaInfo?.RunTimeTicks);

            if (outcome is not null && e.Session is not null)
            {
                var client = _clientIntelligence.GetCachedClient(e.Session.DeviceId)
                             ?? _clientIntelligence.ResolveClient(e.Session);
                _learningService.ProcessOutcome(outcome, client);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback stop for session {PlaySessionId}", e.PlaySessionId);
        }
    }

    private void OnSessionEnded(object? sender, SessionEventArgs e)
    {
        try
        {
            var deviceId = e.SessionInfo?.DeviceId;
            if (!string.IsNullOrEmpty(deviceId))
            {
                _logger.LogDebug("Session ended for {DeviceId} — clearing caches", deviceId);
                _clientIntelligence.InvalidateCache(deviceId);
                _profileManager.ClearAppliedProfile(deviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling session end for {DeviceId}", e.SessionInfo?.DeviceId);
        }
    }
}
