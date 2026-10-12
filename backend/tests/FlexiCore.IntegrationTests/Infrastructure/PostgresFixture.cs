using Npgsql;
using Testcontainers.PostgreSql;

namespace FlexiCore.IntegrationTests.Infrastructure;

public sealed record TestDatabase(string Name, string OwnerConnectionString, string AppConnectionString);

/// <summary>One real PostgreSQL container per test run; each test gets its own isolated database.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string OwnerPassword = "test-owner-pw";
    private const string AppPassword = "test-app-pw";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithUsername("postgres")
        .WithPassword("test-superuser-pw")
        .WithDatabase("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await ExecuteAsAdminAsync(
            $"create role flexicore_owner login password '{OwnerPassword}' nosuperuser nocreatedb nocreaterole",
            $"create role flexicore_app login password '{AppPassword}' nosuperuser nocreatedb nocreaterole");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<TestDatabase> CreateDatabaseAsync()
    {
        var name = "t_" + Guid.NewGuid().ToString("N");
        // CREATE DATABASE cannot share a transaction, so each statement is its own command.
        await ExecuteAsAdminAsync(
            $"create database \"{name}\" owner flexicore_owner",
            $"grant connect on database \"{name}\" to flexicore_app");

        string Build(string user, string password) => new NpgsqlConnectionStringBuilder
        {
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(5432),
            Database = name,
            Username = user,
            Password = password,
            Timeout = 5
        }.ConnectionString;

        return new TestDatabase(name, Build("flexicore_owner", OwnerPassword), Build("flexicore_app", AppPassword));
    }

    private async Task ExecuteAsAdminAsync(params string[] statements)
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();
        foreach (var sql in statements)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;

public static class Sql
{
    public static async Task ExecAsync(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        var value = await cmd.ExecuteScalarAsync();
        return value is DBNull ? null : value;
    }
}