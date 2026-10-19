using FlexiCore.Devices;
using FlexiCore.Devices.Data;
using FlexiCore.Devices.Security;
using Npgsql;

namespace FlexiCore.Api.Licensing;

/// <summary>
/// Administrator command: <c>dotnet FlexiCore.Api.dll pairing-code [--owner]</c>. It runs where the administrator
/// already has access to the machine or container, prints a one-time code and exits without starting the server.
/// It uses ConnectionStrings__Default from the environment and never prints it.
/// </summary>
internal static class PairingCodeCommand
{
    public const string Name = "pairing-code";

    public static async Task<int> RunAsync(string[] args)
    {
        var owner = false;
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--owner", StringComparison.Ordinal)) owner = true;
            else
            {
                await Console.Error.WriteLineAsync("Usage: pairing-code [--owner]");
                return 2;
            }
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await Console.Error.WriteLineAsync("ConnectionStrings__Default is not set in this environment.");
            return 2;
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            var store = new DeviceStore(dataSource, new DeviceAuditWriter(dataSource));
            var result = await store.CreatePairingCodeAsync(
                owner, createdByDeviceId: null, "administrator", TimeProvider.System.GetUtcNow(),
                DevicesModule.AdminPairingCodeLifetime, correlationId: null, CancellationToken.None);

            if (result.Outcome != CodeOutcome.Created)
            {
                await Console.Error.WriteLineAsync("Too many unused pairing codes exist. Wait for them to expire or use one.");
                return 1;
            }

            Console.WriteLine("Pairing code: " + PairingCode.Format(result.Code!));
            Console.WriteLine("Type:         " + (owner ? "owner device" : "ordinary device"));
            Console.WriteLine("Valid until:  " + result.ExpiresAt.ToString("u") + " (single use)");
            return 0;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            await Console.Error.WriteLineAsync(
                "Could not create a pairing code (" + ex.GetType().Name + "). Check that the database is reachable and migrated.");
            return 1;
        }
    }
}