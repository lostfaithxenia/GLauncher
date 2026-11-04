using System.Globalization;
using System.Text.Json;
using GLauncher.Core.Games;
using GLauncher.Core.Storage;
using Microsoft.Data.Sqlite;

namespace GLauncher.Storage.Sqlite;

/// <summary>Local data only. One atomic statement per edit, parameterized SQL, WAL, no game scanning.</summary>
public sealed class SqliteLibraryStore : IGameLibraryStore
{
    private readonly string connectionString;
    private readonly SemaphoreSlim initialization = new(1);
    private bool initialized;
    public SqliteLibraryStore(string databasePath)
    {
        string path = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, DefaultTimeout = 5 }.ToString();
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            await initialization.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!initialized)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = """
                        PRAGMA journal_mode=WAL;
                        CREATE TABLE IF NOT EXISTS games (
                          id TEXT PRIMARY KEY NOT NULL, title TEXT NOT NULL, platform INTEGER NOT NULL,
                          executable TEXT NOT NULL, arguments TEXT, working_directory TEXT,
                          added_at TEXT NOT NULL, description TEXT, cover_path TEXT, last_played_at TEXT);
                        CREATE TABLE IF NOT EXISTS preferences (key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL);
                        PRAGMA user_version=1;
                        """;
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    initialized = true;
                }
            }
            finally { initialization.Release(); }
            return connection;
        }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private static GameEntry Read(SqliteDataReader reader)
    {
        string? Optional(int index) => reader.IsDBNull(index) ? null : reader.GetString(index);
        return new(new GameId(Guid.Parse(reader.GetString(0))), reader.GetString(1), (GamePlatform)reader.GetInt32(2),
            new LaunchTarget(reader.GetString(3), Optional(4), Optional(5)),
            DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture), Optional(7), Optional(8),
            Optional(9) is { } played ? DateTimeOffset.Parse(played, CultureInfo.InvariantCulture) : null);
    }

    public async Task<IReadOnlyList<GameEntry>> GetAllAsync(CancellationToken cancellationToken)
    {
        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT * FROM games ORDER BY title COLLATE NOCASE, id";
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<GameEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) entries.Add(Read(reader));
        return entries;
    }

    public async Task<GameEntry?> GetAsync(GameId id, CancellationToken cancellationToken)
    {
        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT * FROM games WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    public async Task UpsertAsync(GameEntry entry, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Launch.ExecutablePath);
        if (!Enum.IsDefined(entry.Platform)) throw new ArgumentOutOfRangeException(nameof(entry));
        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO games VALUES ($id,$title,$platform,$exe,$arguments,$working,$added,$description,$cover,$played)
            ON CONFLICT(id) DO UPDATE SET title=excluded.title, platform=excluded.platform,
              executable=excluded.executable, arguments=excluded.arguments, working_directory=excluded.working_directory,
              description=excluded.description, cover_path=excluded.cover_path, last_played_at=excluded.last_played_at;
            """;
        void Set(string key, object? value) => command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        Set("$id", entry.Id.ToString()); Set("$title", entry.Title.Trim()); Set("$platform", (int)entry.Platform);
        Set("$exe", entry.Launch.ExecutablePath); Set("$arguments", entry.Launch.Arguments); Set("$working", entry.Launch.WorkingDirectory);
        Set("$added", entry.AddedAt.ToString("O")); Set("$description", entry.Description); Set("$cover", entry.CoverPath);
        Set("$played", entry.LastPlayedAt?.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(GameId id, CancellationToken cancellationToken)
    {
        using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        using var command = connection.CreateCommand(); command.CommandText = "DELETE FROM games WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString()); await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> GetPreferenceAsync<T>(string key, CancellationToken token = default)
    {
        using var connection = await OpenAsync(token).ConfigureAwait(false);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT value FROM preferences WHERE key=$key";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(token).ConfigureAwait(false) is string json ? JsonSerializer.Deserialize<T>(json) : default;
    }

    public async Task SetPreferenceAsync<T>(string key, T value, CancellationToken token = default)
    {
        using var connection = await OpenAsync(token).ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO preferences VALUES ($key,$value) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
        command.Parameters.AddWithValue("$key", key); command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(value));
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}
