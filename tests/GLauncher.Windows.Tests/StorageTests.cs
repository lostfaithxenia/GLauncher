using GLauncher.Core.Games;
using GLauncher.Storage.Sqlite;

namespace GLauncher.Windows.Tests;

public sealed class StorageTests
{
    [Fact]
    public async Task LibraryRoundTripEditAndDeleteSurviveReopening()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GLauncher-fixture-" + Guid.NewGuid());
        string path = Path.Combine(directory, "library.db");
        try
        {
            var store = new SqliteLibraryStore(path);
            var game = new GameEntry(GameId.New(), "SYN's game; DROP TABLE games;", GamePlatform.Ps3,
                new("C:\\Games\\test.exe", "--quoted \"two words\"", "C:\\Games"), DateTimeOffset.UtcNow, "Edited description", "cover.png");
            await store.UpsertAsync(game, default);
            var reopened = new SqliteLibraryStore(path); Assert.Equal(game, await reopened.GetAsync(game.Id, default));
            var changed = game with { Title = "A different title", Platform = GamePlatform.Mobile, LastPlayedAt = DateTimeOffset.UtcNow };
            await reopened.UpsertAsync(changed, default);
            Assert.Equal(changed, Assert.Single(await store.GetAllAsync(default)));
            await store.DeleteAsync(game.Id, default); Assert.Empty(await reopened.GetAllAsync(default));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CancelledEditPreservesThePreviousCompleteEntryAndSettingsPersist()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GLauncher-fixture-" + Guid.NewGuid());
        string path = Path.Combine(directory, "library.db");
        try
        {
            var store = new SqliteLibraryStore(path);
            var game = new GameEntry(GameId.New(), "Before", GamePlatform.Pc, new("fixture.exe"), DateTimeOffset.UtcNow);
            await store.UpsertAsync(game, default);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpsertAsync(game with { Title = "After" }, new CancellationToken(true)));
            Assert.Equal(game, await store.GetAsync(game.Id, default));
            await store.SetPreferenceAsync("theme", "Light");
            Assert.Equal("Light", await new SqliteLibraryStore(path).GetPreferenceAsync<string>("theme"));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void RecommendationsAreOwnedOnlyAndUseDocumentedLocalOrder()
    {
        var old = new GameEntry(GameId.New(), "Old", GamePlatform.Pc, new("old.exe"), DateTimeOffset.UtcNow.AddDays(-5), LastPlayedAt: DateTimeOffset.UtcNow);
        var fresh = old with { Id = GameId.New(), Title = "New", AddedAt = DateTimeOffset.UtcNow, LastPlayedAt = null };
        var result = new LocalRecommendations().Recommend([old, fresh], 8);
        Assert.Equal(fresh.Id, result[0].GameId); Assert.Equal(2, result.Count);
        Assert.Empty(new LocalRecommendations().Recommend([old], 0));
    }
}
