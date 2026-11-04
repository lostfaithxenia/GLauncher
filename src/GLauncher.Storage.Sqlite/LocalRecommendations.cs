using GLauncher.Core.Games;

namespace GLauncher.Storage.Sqlite;

/// <summary>Never-played games first (newest additions), then least recently played; title breaks ties.</summary>
public sealed class LocalRecommendations : IOwnedGameRecommender
{
    public IReadOnlyList<OwnedRecommendation> Recommend(IReadOnlyList<GameEntry> library, int maxCount)
        => library.OrderBy(g => g.LastPlayedAt.HasValue).ThenBy(g => g.LastPlayedAt)
            .ThenByDescending(g => g.AddedAt).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, maxCount)).Select((g, index) => new OwnedRecommendation(g.Id, 1.0 / (index + 1),
                [g.LastPlayedAt is null ? "Ready for your first launch" : "Rediscover your library"])).ToArray();
}
