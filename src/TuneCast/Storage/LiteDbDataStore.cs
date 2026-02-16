using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TuneCast.Models;
using LiteDB;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace TuneCast.Storage;

/// <summary>
/// LiteDB-backed persistent storage for plugin data.
/// Stored in the plugin's configuration directory — never alongside media.
/// </summary>
public class LiteDbDataStore : IPluginDataStore
{
    private const string DatabaseFileName = "tunecast.db";
    private const string ClientCollection = "clients";
    private const string OutcomeCollection = "outcomes";
    private const string InterventionCollection = "interventions";

    private readonly LiteDatabase _db;
    private readonly ILogger<LiteDbDataStore> _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteDbDataStore"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths for locating the plugin data directory.</param>
    /// <param name="logger">Logger instance.</param>
    public LiteDbDataStore(IApplicationPaths applicationPaths, ILogger<LiteDbDataStore> logger)
    {
        _logger = logger;

        var pluginDataDir = Path.Combine(applicationPaths.PluginConfigurationsPath, "TuneCast");
        Directory.CreateDirectory(pluginDataDir);

        var dbPath = Path.Combine(pluginDataDir, DatabaseFileName);
        _db = new LiteDatabase($"Filename={dbPath};Connection=shared");

        EnsureIndexes();
        _logger.LogInformation("TuneCast data store initialized at {Path}", dbPath);
    }

    /// <inheritdoc />
    public ClientModel? GetClientModel(string deviceId)
    {
        var col = _db.GetCollection<ClientModel>(ClientCollection);
        return col.FindOne(c => c.DeviceId == deviceId);
    }

    /// <inheritdoc />
    public void UpsertClientModel(ClientModel client)
    {
        client.LastUpdated = DateTime.UtcNow;
        var col = _db.GetCollection<ClientModel>(ClientCollection);
        var existing = col.FindOne(c => c.DeviceId == client.DeviceId);
        if (existing is not null)
        {
            col.Update(client);
        }
        else
        {
            col.Insert(client);
        }
    }

    /// <inheritdoc />
    public IEnumerable<ClientModel> GetAllClientModels()
    {
        return _db.GetCollection<ClientModel>(ClientCollection).FindAll();
    }

    /// <inheritdoc />
    public void RecordOutcome(PlaybackOutcome outcome)
    {
        var col = _db.GetCollection<PlaybackOutcome>(OutcomeCollection);
        col.Insert(outcome);
    }

    /// <inheritdoc />
    public IEnumerable<PlaybackOutcome> GetOutcomesByDevice(string deviceId, int limit = 100)
    {
        var col = _db.GetCollection<PlaybackOutcome>(OutcomeCollection);
        return col.Find(o => o.DeviceId == deviceId, limit: limit)
                  .OrderByDescending(o => o.Timestamp)
                  .ToList();
    }

    /// <inheritdoc />
    public IEnumerable<PlaybackOutcome> GetOutcomesSince(DateTime since)
    {
        var col = _db.GetCollection<PlaybackOutcome>(OutcomeCollection);
        return col.Find(o => o.Timestamp >= since)
                  .OrderByDescending(o => o.Timestamp)
                  .ToList();
    }

    /// <inheritdoc />
    public int PruneOutcomes(DateTime olderThan)
    {
        var col = _db.GetCollection<PlaybackOutcome>(OutcomeCollection);
        int deleted = col.DeleteMany(o => o.Timestamp < olderThan);
        _logger.LogInformation("Pruned {Count} telemetry records older than {Date}", deleted, olderThan);
        return deleted;
    }

    /// <inheritdoc />
    public void RecordIntervention(InterventionRecord record)
    {
        var col = _db.GetCollection<InterventionRecord>(InterventionCollection);
        col.Insert(record);
    }

    /// <inheritdoc />
    public IEnumerable<InterventionRecord> GetInterventionsSince(DateTime since)
    {
        var col = _db.GetCollection<InterventionRecord>(InterventionCollection);
        return col.Find(r => r.Timestamp >= since)
                  .OrderByDescending(r => r.Timestamp)
                  .ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _db.Dispose();
            _disposed = true;
        }
    }

    private void EnsureIndexes()
    {
        var clients = _db.GetCollection<ClientModel>(ClientCollection);
        clients.EnsureIndex(c => c.DeviceId, true);

        var outcomes = _db.GetCollection<PlaybackOutcome>(OutcomeCollection);
        outcomes.EnsureIndex(o => o.DeviceId);
        outcomes.EnsureIndex(o => o.Timestamp);
        outcomes.EnsureIndex(o => o.PlaySessionId);

        var interventions = _db.GetCollection<InterventionRecord>(InterventionCollection);
        interventions.EnsureIndex(i => i.Timestamp);
        interventions.EnsureIndex(i => i.DeviceId);
    }
}
