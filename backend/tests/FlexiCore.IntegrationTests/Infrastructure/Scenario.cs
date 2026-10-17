using System.Net;
using System.Security.Cryptography;
using System.Text;
using FlexiCore.Identity.Security;
using FlexiCore.Identity.Settings;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;

namespace FlexiCore.IntegrationTests.Infrastructure;

internal static class Scenario
{
    public const string Email = "Alice.Smith@Example.com"; // the password below must not contain "alice.smith"
    public const string Password = "Tr0ub4dor&3-horse-staple";
    public const string WrongPassword = "wrong-password-123";

    public sealed record Session(string AccessToken, string RefreshToken);

    public static async Task<IdentityTestApp> StartWithUserAsync(
        PostgresFixture postgres, string rateLimitPerMinute = "100000", Action<IServiceCollection>? configureServices = null)
    {
        var app = await IdentityTestApp.StartAsync(postgres, "true", rateLimitPerMinute, configureServices);
        if (string.Equals(
                Environment.GetEnvironmentVariable("Deployment__Mode"), "Local", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = EmailRules.Normalize(Email);
            var passwordHash = new PasswordService(IdentitySettings.DefaultPbkdf2Iterations).Hash(Password);
            await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "insert into identity.users (email, email_normalized, password_hash) values (@email, @normalized, @hash)",
                connection);
            command.Parameters.AddWithValue("email", Email);
            command.Parameters.AddWithValue("normalized", normalized);
            command.Parameters.AddWithValue("hash", passwordHash);
            await command.ExecuteNonQueryAsync();
            return app;
        }

        var register = await app.RegisterAsync(Email, Password);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        return app;
    }

    public static async Task<Session> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await IdentityTestApp.ReadJsonAsync(response);
        return new Session(json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }

    public static async Task<Session> LoginAsync(IdentityTestApp app, string email = Email, string password = Password) =>
        await ReadSessionAsync(await app.LoginAsync(email, password));

    public static string HashHex(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(refreshToken))).ToLowerInvariant();
}