using MongoDB.Driver;
using Npgsql;
using Respawn;
using StackExchange.Redis;

namespace Cashflow.TestSupport;

public static class DatabaseReset
{
    private static readonly RespawnerOptions PostgresOptions = new()
    {
        DbAdapter = DbAdapter.Postgres,
        SchemasToInclude = new[] { "ledger", "messaging" },
        TablesToIgnore = new[] { new Respawn.Graph.Table("__EFMigrationsHistory") },
        WithReseed = false
    };

    public static async Task<Respawner> CreatePostgresRespawnerAsync(string connectionString)
    {
        var conn = new NpgsqlConnection(connectionString);
        await using (conn.ConfigureAwait(false))
        {
            await conn.OpenAsync().ConfigureAwait(false);
        return await Respawner.CreateAsync(conn, PostgresOptions).ConfigureAwait(false);
        }
    }

    public static async Task ResetPostgresAsync(Respawner respawner, string connectionString)
    {
        var conn = new NpgsqlConnection(connectionString);
        await using (conn.ConfigureAwait(false))
        {
            await conn.OpenAsync().ConfigureAwait(false);
        await respawner.ResetAsync(conn).ConfigureAwait(false);
        }
    }

    public static async Task ResetMongoAsync(string connectionString, string database)
    {
        var client = new MongoClient(connectionString);
        await client.DropDatabaseAsync(database).ConfigureAwait(false);
    }

    public static async Task ResetRedisAsync(string connectionString)
    {
        var configuration = ConfigurationOptions.Parse(connectionString);
        configuration.AllowAdmin = true;
        using var mux = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        var endpoints = mux.GetEndPoints();
        foreach (var endpoint in endpoints)
        {
            var server = mux.GetServer(endpoint);
            await server.FlushAllDatabasesAsync().ConfigureAwait(false);
        }
    }
}
