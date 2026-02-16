using System;
using System.Collections.Generic;
using TuneCast.Models;

namespace TuneCast.Storage;

/// <summary>
/// Persistent storage abstraction for plugin data.
/// Backed by LiteDB in the default implementation.
/// </summary>
public interface IPluginDataStore : IDisposable
{
    /// <summary>
    /// Retrieves a stored client model by device identifier.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <returns>The client model, or null if not found.</returns>
    ClientModel? GetClientModel(string deviceId);

    /// <summary>
    /// Persists or updates a client model.
    /// </summary>
    /// <param name="client">The client model to store.</param>
    void UpsertClientModel(ClientModel client);

    /// <summary>
    /// Retrieves all stored client models.
    /// </summary>
    /// <returns>All client models.</returns>
    IEnumerable<ClientModel> GetAllClientModels();

    /// <summary>
    /// Records a playback outcome for telemetry.
    /// </summary>
    /// <param name="outcome">The outcome to record.</param>
    void RecordOutcome(PlaybackOutcome outcome);

    /// <summary>
    /// Retrieves playback outcomes for a specific device.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <returns>Recent outcomes for the device.</returns>
    IEnumerable<PlaybackOutcome> GetOutcomesByDevice(string deviceId, int limit = 100);

    /// <summary>
    /// Retrieves all playback outcomes within a time range.
    /// </summary>
    /// <param name="since">Start of the time range (UTC).</param>
    /// <returns>Outcomes since the specified time.</returns>
    IEnumerable<PlaybackOutcome> GetOutcomesSince(DateTime since);

    /// <summary>
    /// Deletes playback outcomes older than the specified date.
    /// </summary>
    /// <param name="olderThan">Cutoff date (UTC).</param>
    /// <returns>Number of records deleted.</returns>
    int PruneOutcomes(DateTime olderThan);

    /// <summary>
    /// Records a policy intervention event.
    /// </summary>
    /// <param name="record">The intervention record to store.</param>
    void RecordIntervention(InterventionRecord record);

    /// <summary>
    /// Retrieves intervention records within a time range.
    /// </summary>
    /// <param name="since">Start of the time range (UTC).</param>
    /// <returns>Interventions since the specified time, newest first.</returns>
    IEnumerable<InterventionRecord> GetInterventionsSince(DateTime since);
}
