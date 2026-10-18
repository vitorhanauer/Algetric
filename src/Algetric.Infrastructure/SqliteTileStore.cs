using System.Text.Json;
using Microsoft.Data.Sqlite;
using Algetric.Core;

namespace Algetric.Infrastructure;

public sealed class SqliteTileStore : ITileStore
{
    private readonly string connectionString;
    public SqliteTileStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS tiles(provider TEXT,z INTEGER,x INTEGER,y INTEGER,data BLOB,type TEXT,
                PRIMARY KEY(provider,z,x,y));
            CREATE TABLE IF NOT EXISTS regions(id TEXT PRIMARY KEY,json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS region_tiles(region TEXT REFERENCES regions(id) ON DELETE CASCADE,
                provider TEXT,z INTEGER,x INTEGER,y INTEGER,PRIMARY KEY(region,provider,z,x,y));
            """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var connection = new SqliteConnection(connectionString); connection.Open(); return connection; }
    public async Task<TileData?> GetAsync(string provider, TileAddress tile)
    {
        using var connection = Open(); using var command = TileCommand(connection, provider, tile);
        command.CommandText = "SELECT data,type FROM tiles WHERE provider=$p AND z=$z AND x=$x AND y=$y";
        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? new((byte[])reader[0], reader.GetString(1)) : null;
    }
    public async Task PutAsync(string provider, TileAddress tile, byte[] bytes, string contentType, Guid? regionId = null)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        using var command = TileCommand(connection, provider, tile); command.Transaction = transaction;
        command.CommandText = "INSERT OR REPLACE INTO tiles VALUES($p,$z,$x,$y,$data,$type)";
        command.Parameters.AddWithValue("$data", bytes); command.Parameters.AddWithValue("$type", contentType);
        await command.ExecuteNonQueryAsync();
        if (regionId is { } id)
        {
            command.CommandText = "INSERT OR IGNORE INTO region_tiles VALUES($r,$p,$z,$x,$y)";
            command.Parameters.AddWithValue("$r", id.ToString()); await command.ExecuteNonQueryAsync();
        }
        transaction.Commit();
    }
    public async Task SaveRegionAsync(OfflineRegion region)
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO regions VALUES($id,$json) ON CONFLICT(id) DO UPDATE SET json=excluded.json";
        command.Parameters.AddWithValue("$id", region.Id.ToString());
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(region)); await command.ExecuteNonQueryAsync();
    }
    public async Task<IReadOnlyList<OfflineRegion>> ListRegionsAsync()
    {
        using var connection = Open(); using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM regions"; using var reader = await command.ExecuteReaderAsync();
        var regions = new List<OfflineRegion>();
        while (await reader.ReadAsync()) regions.Add(JsonSerializer.Deserialize<OfflineRegion>(reader.GetString(0))!);
        return regions;
    }
    public async Task RemoveRegionAsync(Guid id)
    {
        using var connection = Open(); using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "DELETE FROM regions WHERE id=$id"; command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync();
        command.CommandText = """
            DELETE FROM tiles WHERE NOT EXISTS(SELECT 1 FROM region_tiles r
                WHERE r.provider=tiles.provider AND r.z=tiles.z AND r.x=tiles.x AND r.y=tiles.y)
            """;
        await command.ExecuteNonQueryAsync(); transaction.Commit();
    }
    private static SqliteCommand TileCommand(SqliteConnection connection, string provider, TileAddress tile)
    {
        var command = connection.CreateCommand();
        command.Parameters.AddWithValue("$p", provider); command.Parameters.AddWithValue("$z", tile.Zoom);
        command.Parameters.AddWithValue("$x", tile.X); command.Parameters.AddWithValue("$y", tile.Y); return command;
    }
}
