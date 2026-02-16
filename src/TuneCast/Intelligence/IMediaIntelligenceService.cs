using TuneCast.Models;
using MediaBrowser.Model.Dto;

namespace TuneCast.Intelligence;

/// <summary>
/// Analyzes media sources and produces normalized media models with transcode cost estimates.
/// </summary>
public interface IMediaIntelligenceService
{
    /// <summary>
    /// Resolves a <see cref="MediaModel"/> from a Jellyfin media source.
    /// Results are cached per media source ID.
    /// </summary>
    /// <param name="mediaSource">The Jellyfin media source info.</param>
    /// <param name="itemId">The Jellyfin item identifier.</param>
    /// <returns>A normalized media model.</returns>
    MediaModel ResolveMedia(MediaSourceInfo mediaSource, string itemId);

    /// <summary>
    /// Retrieves a cached media model by media source identifier.
    /// </summary>
    /// <param name="mediaSourceId">The media source identifier.</param>
    /// <returns>The cached media model, or null if not found.</returns>
    MediaModel? GetCachedMedia(string mediaSourceId);

    /// <summary>
    /// Invalidates all cached media models (e.g. after library scan).
    /// </summary>
    void InvalidateAllCache();
}
