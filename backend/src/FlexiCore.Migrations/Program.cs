using FlexiCore.Migrations;

var connectionString = Environment.GetEnvironmentVariable("FLEXICORE_MIGRATIONS_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("FLEXICORE_MIGRATIONS_CONNECTION is not set. It must be a connection string for the flexicore_owner role.");
    return 2;
}

try
{
    var applied = await new MigrationRunner(connectionString, Console.WriteLine).RunAsync();
    Console.WriteLine($"Done. Applied {applied} migration(s).");
    return 0;
}
catch (Exception ex)
{
    // Message only: never echo the connection string.
    Console.Error.WriteLine($"Migration failed: {ex.Message}");
    return 1;
}