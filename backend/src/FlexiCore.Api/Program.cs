using FlexiCore.Api.Health;
using FlexiCore.Api.Observability;
using FlexiCore.Identity;
using FlexiCore.Tenancy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Npgsql;

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
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
// RFC 9457 problem details; unhandled exceptions produce a generic 500 with no stack trace or exception text.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Liveness: the process is up. Runs no dependency checks.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
// Readiness: dependencies (PostgreSQL) are reachable.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.MapIdentityEndpoints();

app.Run();

public partial class Program;