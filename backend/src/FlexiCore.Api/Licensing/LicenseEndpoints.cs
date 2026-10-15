using FlexiCore.Licensing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace FlexiCore.Api.Licensing;

public sealed record LicenseDetails(
    string LicenseId,
    string Licensee,
    string Product,
    DateTimeOffset IssuedAt,
    DateTimeOffset NotBefore,
    DateTimeOffset? ExpiresAt,
    int GraceDays,
    DateTimeOffset? GraceEndsAt,
    int MaxInstallations);

public sealed record LicenseStatusResponse(
    string Mode,
    bool LicenseRequired,
    string State,
    string? Reason,
    LicenseDetails? License,
    DateTimeOffset CheckedAt);

public sealed record LicenseImportResponse(
    string Mode,
    bool LicenseRequired,
    string State,
    string? Reason,
    LicenseDetails? License,
    DateTimeOffset CheckedAt,
    bool Replaced);

internal static class LicenseEndpoints
{
    private const string ProblemPrefix = "urn:flexicore:problem:";

    public static void Map(IEndpointRouteBuilder endpoints, DeploymentSettings settings)
    {
        // Any authenticated user may call these. That is an interim development rule, not the commercial security model.
        var group = endpoints.MapGroup("/api/v1/license").RequireAuthorization();

        if (settings.Mode == DeploymentMode.Hosted)
        {
            group.MapGet("/status", HostedStatus);
            return; // no import route in Hosted mode, so PUT returns 404
        }

        group.MapGet("/status", LocalStatusAsync);
        group.MapPut("", ImportAsync).RequireRateLimiting(LicensingModule.ImportRateLimitPolicy);
    }

    private static IResult HostedStatus(HttpContext http, TimeProvider time)
    {
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new LicenseStatusResponse("hosted", false, "notApplicable", null, null, time.GetUtcNow()));
    }

    private static async Task<IResult> LocalStatusAsync(HttpContext http, LicenseService service, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var report = await service.GetStatusAsync(ct);
        return Results.Ok(ToStatus(report));
    }

    private static async Task<IResult> ImportAsync(
        HttpContext http, LicenseService service, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var logger = loggerFactory.CreateLogger("FlexiCore.Licensing");
        var correlationId = http.TraceIdentifier;

        if (!MediaTypeHeaderValue.TryParse(http.Request.ContentType, out var mediaType)
            || !string.Equals(mediaType.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase))
            return Problem(StatusCodes.Status415UnsupportedMediaType, "License file must be sent as application/json",
                "license-unsupported-media-type");

        if (http.Request.ContentLength is > LicenseLimits.MaxFileBytes)
            return TooLarge();

        // Read at most the limit plus one byte, so an oversized or endless body is never buffered in full.
        var buffer = new byte[LicenseLimits.MaxFileBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await http.Request.Body.ReadAsync(buffer.AsMemory(total), ct);
            if (read == 0) break;
            total += read;
        }
        if (total > LicenseLimits.MaxFileBytes) return TooLarge();

        var result = await service.ImportAsync(buffer.AsSpan(0, total).ToArray(), ct);

        switch (result.Outcome)
        {
            case LicenseImportOutcome.Imported:
            case LicenseImportOutcome.Unchanged:
            {
                var replaced = result.Outcome == LicenseImportOutcome.Imported;
                var report = result.Report!;
                logger.LogInformation(
                    "License import {Outcome} for license {LicenseId} (correlation {CorrelationId})",
                    result.Outcome, report.License?.LicenseId, correlationId);
                return Results.Ok(ToImport(report, replaced));
            }

            case LicenseImportOutcome.OlderThanCurrent:
                logger.LogWarning("License import refused: older than the installed license (correlation {CorrelationId})", correlationId);
                return Problem(StatusCodes.Status409Conflict, "A newer license is already installed", "license-older-than-current");

            case LicenseImportOutcome.VerificationUnavailable:
                logger.LogWarning("License import refused: no trusted keys (correlation {CorrelationId})", correlationId);
                return Problem(StatusCodes.Status503ServiceUnavailable, "License verification is unavailable",
                    "license-verification-unavailable", LicenseService.ReasonCode(result.Verification));

            case LicenseImportOutcome.StorageFailed:
                logger.LogError("License import failed: storage error (correlation {CorrelationId})", correlationId);
                return Problem(StatusCodes.Status503ServiceUnavailable, "The license could not be stored", "license-storage-failed");

            default:
                logger.LogWarning(
                    "License import rejected: {Reason} (correlation {CorrelationId})",
                    LicenseService.ReasonCode(result.Verification), correlationId);
                return RejectedProblem(result.Verification);
        }
    }

    private static IResult RejectedProblem(LicenseStatus status) => status switch
    {
        LicenseStatus.FileTooLarge => TooLarge(),
        LicenseStatus.UnsupportedSchemaVersion =>
            Problem(StatusCodes.Status400BadRequest, "License file version is not supported",
                "license-unsupported-version", LicenseService.ReasonCode(status)),
        LicenseStatus.Malformed or LicenseStatus.InvalidPayload =>
            Problem(StatusCodes.Status400BadRequest, "License file is malformed or invalid",
                "license-malformed", LicenseService.ReasonCode(status)),
        _ =>
            Problem(StatusCodes.Status422UnprocessableEntity, "The license was rejected",
                "license-rejected", LicenseService.ReasonCode(status)),
    };

    private static IResult TooLarge() =>
        Problem(StatusCodes.Status413PayloadTooLarge, "License file is too large", "license-too-large",
            LicenseService.ReasonCode(LicenseStatus.FileTooLarge));

    // Problem bodies never include any part of the submitted file.
    private static IResult Problem(int status, string title, string slug, string? reason = null) =>
        Results.Problem(
            statusCode: status,
            title: title,
            type: ProblemPrefix + slug,
            extensions: reason is null ? null : new Dictionary<string, object?> { ["reason"] = reason });

    private static LicenseStatusResponse ToStatus(LicenseStatusReport r) =>
        new("local", true, StateName(r.State), r.Reason, ToDetails(r), r.CheckedAt);

    private static LicenseImportResponse ToImport(LicenseStatusReport r, bool replaced) =>
        new("local", true, StateName(r.State), r.Reason, ToDetails(r), r.CheckedAt, replaced);

    private static LicenseDetails? ToDetails(LicenseStatusReport r) =>
        r.License is { } p
            ? new LicenseDetails(p.LicenseId, p.Licensee, p.Product, p.IssuedAt, p.NotBefore, p.ExpiresAt,
                p.GraceDays, r.GraceEndsAt, p.MaxInstallations)
            : null;

    private static string StateName(LicenseState state) => state switch
    {
        LicenseState.NotApplicable => "notApplicable",
        LicenseState.Missing => "missing",
        LicenseState.Valid => "valid",
        LicenseState.ValidInGrace => "validInGrace",
        LicenseState.Expired => "expired",
        LicenseState.NotYetValid => "notYetValid",
        LicenseState.Invalid => "invalid",
        LicenseState.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };
}