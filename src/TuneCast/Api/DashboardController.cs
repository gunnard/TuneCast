using System;
using System.Collections.Generic;
using System.Linq;
using TuneCast.Configuration;
using TuneCast.Models;
using TuneCast.Storage;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TuneCast.Api;

/// <summary>
/// API controller for the TuneCast dashboard.
/// </summary>
[ApiController]
[Route("TuneCast/Dashboard")]
[Authorize(Policy = "RequiresElevation")]
public class DashboardController : ControllerBase
{
    private readonly IPluginDataStore _store;
    private readonly ISessionManager _sessionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardController"/> class.
    /// </summary>
    /// <param name="store">Plugin data store.</param>
    /// <param name="sessionManager">Jellyfin session manager.</param>
    public DashboardController(IPluginDataStore store, ISessionManager sessionManager)
    {
        _store = store;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Gets all known clients with their capabilities and confidence scores.
    /// </summary>
    /// <returns>List of client models.</returns>
    [HttpGet("Clients")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<ClientModel>> GetClients()
    {
        var clients = _store.GetAllClientModels().ToList();
        return Ok(clients);
    }

    /// <summary>
    /// Gets recent playback telemetry outcomes.
    /// </summary>
    /// <param name="hours">Number of hours to look back (default 24).</param>
    /// <returns>List of playback outcomes.</returns>
    [HttpGet("Telemetry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<PlaybackOutcome>> GetTelemetry([FromQuery] int hours = 24)
    {
        var since = DateTime.UtcNow.AddHours(-Math.Max(hours, 1));
        var outcomes = _store.GetOutcomesSince(since).ToList();
        return Ok(outcomes);
    }

    /// <summary>
    /// Gets recent intervention records showing when TuneCast influenced playback decisions.
    /// </summary>
    /// <param name="hours">Number of hours to look back (default 24).</param>
    /// <returns>List of intervention records.</returns>
    [HttpGet("Interventions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<InterventionRecord>> GetInterventions([FromQuery] int hours = 24)
    {
        var since = DateTime.UtcNow.AddHours(-Math.Max(hours, 1));
        var interventions = _store.GetInterventionsSince(since).ToList();
        return Ok(interventions);
    }

    /// <summary>
    /// Gets a summary of the plugin state.
    /// </summary>
    /// <returns>Dashboard summary object.</returns>
    [HttpGet("Summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetSummary()
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var clients = _store.GetAllClientModels().ToList();
        var recentOutcomes = _store.GetOutcomesSince(DateTime.UtcNow.AddDays(-7)).ToList();

        var activeSessions = _sessionManager.Sessions
            .Where(s => s.NowPlayingItem is not null)
            .Select(s => new
            {
                s.DeviceId,
                s.DeviceName,
                s.Client,
                ItemName = s.NowPlayingItem?.Name ?? "Unknown",
                PlayMethod = s.PlayState?.PlayMethod?.ToString() ?? "Unknown"
            })
            .ToList();

        var totalSessions = recentOutcomes.Count;
        var directPlayCount = recentOutcomes.Count(o =>
            o.PlayMethod.Equals("DirectPlay", StringComparison.OrdinalIgnoreCase));
        var directStreamCount = recentOutcomes.Count(o =>
            o.PlayMethod.Equals("DirectStream", StringComparison.OrdinalIgnoreCase));
        var transcodeCount = recentOutcomes.Count(o =>
            o.PlayMethod.Equals("Transcode", StringComparison.OrdinalIgnoreCase));
        var failureCount = recentOutcomes.Count(o =>
            o.Result == PlaybackResult.Failure || o.Result == PlaybackResult.SuspectedFailure);

        return Ok(new
        {
            Config = new
            {
                config.ConservativeMode,
                config.EnableDynamicProfiles,
                config.EnableLearning,
                config.EnableVerboseLogging,
                config.GlobalMaxBitrateOverride,
                config.ServerLoadBiasWeight,
                config.TelemetryRetentionDays
            },
            Stats = new
            {
                TotalClients = clients.Count,
                TotalSessions7d = totalSessions,
                DirectPlayCount = directPlayCount,
                DirectStreamCount = directStreamCount,
                TranscodeCount = transcodeCount,
                FailureCount = failureCount,
                DirectPlayRate = totalSessions > 0
                    ? Math.Round(100.0 * directPlayCount / totalSessions, 1)
                    : 0.0
            },
            ActiveSessions = activeSessions
        });
    }
}
