using GLauncher.Core.Games;

namespace GLauncher.Core.Storage;

/// <summary>
/// Persistence boundary. The implementation (SQLite is proposed, not yet benchmarked) lives outside Core.
/// UpsertAsync must be atomic so an interrupted edit never leaves a half-written game.
/// </summary>
public interface IGameLibraryStore
{
    Task<IReadOnlyList<GameEntry>> GetAllAsync(CancellationToken cancellationToken);
    Task<GameEntry?> GetAsync(GameId id, CancellationToken cancellationToken);
    Task UpsertAsync(GameEntry entry, CancellationToken cancellationToken);
    Task DeleteAsync(GameId id, CancellationToken cancellationToken);
}
