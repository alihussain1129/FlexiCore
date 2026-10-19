using FlexiCore.Api.Health;
using FlexiCore.Api.Licensing;
using FlexiCore.Api.Observability;
using FlexiCore.Catalog;
using FlexiCore.Devices;
using FlexiCore.Identity;
using FlexiCore.Tenancy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;

// Administrator command, e.g. "dotnet FlexiCore.Api.dll pairing-code --owner": prints a one-time pairing code and exits.
if (args.Length > 0 && string.Equals(args[0], PairingCodeCommand.Name, StringComparison.Ordinal))
{
    Environment.ExitCode = await PairingCodeCommand.RunAsync(args[1..]);
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.UseUtcTimestamp = true;
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is not configured. In development use 'dotnet user-secrets'; elsewhere use the ConnectionStrings__Default environment variable.");

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddIdentityModule(builder.Configuration, builder.Environment);
builder.Services.AddTenancyModule();
builder.Services.AddCatalogModule();
builder.Services.AddLicensingModule(builder.Configuration);
if (DeploymentSettings.From(builder.Configuration).Mode == DeploymentMode.Local)
{
    // Activation-only edition: paired devices authenticate instead of users. Hosted mode never registers this.
    builder.Services.AddDevicesModule(builder.Configuration);
    builder.Services.AddSingleton<IDeviceActivationGate, LicenseDeviceActivationGate>();
}
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
// RFC 9457 problem details; unhandled exceptions produce a generic 500 with no stack trace or exception text.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
// After authentication so per-user policies can read the caller; before authorization.
app.UseRateLimiter();
// After authentication so per-user policies can read the caller; before authorization.
app.UseAuthorization();
// Local mode: nothing but health and license routes is served until a valid license is installed. Hosted: no-op.
// Run after authorization so unauthenticated requests can be challenged with 401 before a license block is evaluated.
app.UseLicenseEnforcement();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness: the process is up. Runs no dependency checks.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
// Readiness: dependencies (PostgreSQL) are reachable.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.MapIdentityEndpoints();
app.MapTenancyEndpoints();
app.MapCatalogEndpoints();
app.MapLicenseEndpoints();
if (app.Services.GetRequiredService<DeploymentSettings>().Mode == DeploymentMode.Local)
{
    app.MapDeviceEndpoints();
}
LocalSetupEndpoints.Map(app, app.Services.GetRequiredService<DeploymentSettings>());

app.Run();

public partial class Program;