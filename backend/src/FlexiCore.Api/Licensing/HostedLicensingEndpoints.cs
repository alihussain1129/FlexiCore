using System.Security.Claims;
using System.Text.Json;
using FlexiCore.Licensing;
using FlexiCore.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FlexiCore.Api.Licensing;

internal static class HostedLicensingEndpoints
{
    private sealed record RenewalRequest(string? PreviousLicenseId, string? Reason, string? SignedLicenseBase64);
    private sealed record ActivationRequest(Guid RequestId, string LicenseId, string InstallationId,
        string EventType, string? PreviousInstallationId);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/organizations/{organizationId:guid}/licensing").RequireAuthorization();
        group.MapPost("/licenses", RegisterLicenseAsync);
        group.MapPost("/renewals", RenewAsync);
        group.MapPost("/activations", RecordActivationAsync);
        group.MapPut("/revocations", ImportRevocationsAsync);
        group.MapPut("/entitlements", ImportEntitlementsAsync);
        group.MapGet("/entitlements/{moduleKey}/{featureKey}", CheckEntitlementAsync);
        group.MapGet("/sync", SyncAsync);
    }

    private static async Task<IResult> RegisterLicenseAsync(Guid organizationId, HttpContext http,
        ClaimsPrincipal user, LicenseVerifier verifier, LicensingStore store, CancellationToken ct)
    {
        if (!TryActor(user, out var actor) || !Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var requestId)
            || requestId == Guid.Empty) return Problem(400, "A valid Idempotency-Key UUID and authenticated user are required.", "idempotency-key-required");
        var bytes = await ReadBodyAsync(http, LicenseLimits.MaxFileBytes, ct);
        if (bytes is null) return Problem(413, "License artifact exceeds the supported size.", "license-too-large");
        var check = verifier.Verify(bytes);
        if (check.Payload is not { } payload || check.Status is not (LicenseStatus.Valid or LicenseStatus.ValidInGrace))
            return Problem(422, "The signed license is invalid or not currently eligible.", "license-rejected");
        var key = EnvelopeKeyId(bytes);
        if (key is null) return Problem(400, "The signed license envelope is malformed.", "license-malformed");
        var outcome = await store.RegisterLicenseAsync(TenantContext.For(actor, correlationId: Correlation(http)),
            organizationId, actor, requestId, payload.InstallationId, bytes, key, payload, ct);
        return WriteResult(outcome, payload.LicenseId);
    }

    private static async Task<IResult> RenewAsync(Guid organizationId, HttpContext http, ClaimsPrincipal user,
        LicenseVerifier verifier, LicensingStore store, CancellationToken ct)
    {
        if (!TryActor(user, out var actor) || !Guid.TryParse(http.Request.Headers["Idempotency-Key"], out var requestId)
            || requestId == Guid.Empty) return Problem(400, "A valid Idempotency-Key UUID and authenticated user are required.", "idempotency-key-required");
        var body = await ReadJsonAsync<RenewalRequest>(http, ct);
        if (body?.PreviousLicenseId is not { Length: > 0 and <= 64 } previous || string.IsNullOrWhiteSpace(body.Reason)
            || body.SignedLicenseBase64 is null) return Problem(400, "Renewal fields are invalid.", "renewal-invalid");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(body.SignedLicenseBase64); }
        catch (FormatException) { return Problem(400, "Renewal license encoding is invalid.", "license-malformed"); }
        if (bytes.Length > LicenseLimits.MaxFileBytes) return Problem(413, "License artifact exceeds the supported size.", "license-too-large");
        var check = verifier.Verify(bytes);
        if (check.Payload is not { } payload || check.Status is not (LicenseStatus.Valid or LicenseStatus.ValidInGrace))
            return Problem(422, "The renewed license is invalid or not currently eligible.", "license-rejected");
        var key = EnvelopeKeyId(bytes);
        if (key is null) return Problem(400, "The signed license envelope is malformed.", "license-malformed");
        var outcome = await store.RecordRenewalAsync(TenantContext.For(actor, correlationId: Correlation(http)),
            organizationId, actor, requestId, body.Reason, bytes, key, payload, previous, ct);
        return WriteResult(outcome, payload.LicenseId);
    }

    private static async Task<IResult> RecordActivationAsync(Guid organizationId, ActivationRequest request,
        HttpContext http, ClaimsPrincipal user, LicensingStore store, CancellationToken ct)
    {
        if (!TryActor(user, out var actor)) return Results.Unauthorized();
        var outcome = await store.RecordActivationAsync(TenantContext.For(actor, correlationId: Correlation(http)),
            organizationId, actor, request.RequestId, request.LicenseId, request.InstallationId,
            request.EventType, request.PreviousInstallationId, ct);
        return WriteResult(outcome, request.LicenseId);
    }

    private static async Task<IResult> ImportRevocationsAsync(Guid organizationId, HttpContext http,
        ClaimsPrincipal user, SignedRevocationListVerifier verifier, LicensingStore store, CancellationToken ct)
    {
        if (!TryActor(user, out var actor)) return Results.Unauthorized();
        var bytes = await ReadBodyAsync(http, SignedRevocationListVerifier.MaxFileBytes, ct);
        if (bytes is null) return Problem(413, "Revocation artifact exceeds the supported size.", "revocation-too-large");
        var check = verifier.Verify(bytes);
        if (!check.IsValid || check.List is null) return Problem(422, "The signed revocation snapshot was rejected.", "revocation-rejected");
        var key = EnvelopeKeyId(bytes);
        if (key is null) return Problem(400, "The signed snapshot envelope is malformed.", "revocation-malformed");
        var outcome = await store.ImportRevocationSnapshotAsync(TenantContext.For(actor, correlationId: Correlation(http)),
            organizationId, actor, bytes, check.List, key, ct);
        return WriteResult(outcome, check.List.ListId);
    }

    private static async Task<IResult> ImportEntitlementsAsync(Guid organizationId, HttpContext http,
        ClaimsPrincipal user, SignedModuleEntitlementVerifier verifier, LicensingStore store, CancellationToken ct)
    {
        if (!TryActor(user, out var actor)) return Results.Unauthorized();
        var bytes = await ReadBodyAsync(http, SignedModuleEntitlementVerifier.MaxFileBytes, ct);
        if (bytes is null) return Problem(413, "Entitlement artifact exceeds the supported size.", "entitlement-too-large");
        var check = verifier.Verify(bytes);
        if (!check.IsValid) return Problem(422, "The signed entitlement was rejected.", "entitlement-rejected", check.Error.ToString());
        var outcome = await store.ImportEntitlementAsync(TenantContext.For(actor, correlationId: Correlation(http)),
            organizationId, actor, bytes, check, ct);
        return WriteResult(outcome, check.Payload!.LicenseId);
    }

    private static async Task<IResult> CheckEntitlementAsync(Guid organizationId, string moduleKey, string featureKey,
        HttpContext http, ClaimsPrincipal user, IModuleAccessPolicy access, CancellationToken ct)
    {
        if (!TryActor(user, out var actor)) return Results.Unauthorized();
        var result = await access.CheckAsync(actor, organizationId, moduleKey, featureKey, Correlation(http), ct);
        return result.Allowed ? Results.Ok(new { allowed = true }) : Problem(403,
            "The requested module or feature is not available.", "entitlement-required");
    }

    private static async Task<IResult> SyncAsync(Guid organizationId, ClaimsPrincipal user,
        LicensingStore store, HttpContext http, CancellationToken ct)
    {
        if (!TryActor(user, out var actor)) return Results.Unauthorized();
        http.Response.Headers.CacheControl = "no-store";
        var bundle = await store.GetSyncBundleAsync(TenantContext.For(actor, correlationId: Correlation(http)), organizationId, ct);
        return Results.Ok(bundle);
    }

    private static async Task<byte[]?> ReadBodyAsync(HttpContext http, int limit, CancellationToken ct)
    {
        if (http.Request.ContentLength is > 0 && http.Request.ContentLength > limit) return null;
        var buffer = new byte[limit + 1]; var count = 0;
        while (count < buffer.Length)
        {
            var read = await http.Request.Body.ReadAsync(buffer.AsMemory(count), ct);
            if (read == 0) break;
            count += read;
        }
        return count > limit ? null : buffer.AsSpan(0, count).ToArray();
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpContext http, CancellationToken ct)
    {
        var bytes = await ReadBodyAsync(http, 2 * LicenseLimits.MaxFileBytes, ct);
        if (bytes is null || bytes.Length == 0) return default;
        try { return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return default; }
    }

    private static string? EnvelopeKeyId(byte[] artifact)
    {
        try
        {
            using var doc = JsonDocument.Parse(artifact);
            return doc.RootElement.TryGetProperty("keyId", out var key) && key.ValueKind == JsonValueKind.String
                ? key.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static bool TryActor(ClaimsPrincipal user, out Guid actor) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out actor) && actor != Guid.Empty;
    private static string? Correlation(HttpContext http) => http.Items.TryGetValue(
        FlexiCore.SharedKernel.CorrelationContext.ItemKey, out var value) ? value as string : null;

    private static IResult WriteResult(LicensingWriteOutcome outcome, string target) => outcome switch
    {
        LicensingWriteOutcome.Created => Results.Json(new { state = "stored", target }, statusCode: 201),
        LicensingWriteOutcome.Unchanged => Results.Ok(new { state = "unchanged", target }),
        LicensingWriteOutcome.Conflict => Problem(409, "The request conflicts with the current licensing state.", "licensing-conflict"),
        LicensingWriteOutcome.Forbidden => Problem(403, "The organization or licensing operation is unavailable.", "forbidden"),
        _ => Problem(422, "The licensing request is invalid.", "licensing-invalid")
    };

    private static IResult Problem(int status, string detail, string slug, string? reason = null) => Results.Problem(
        statusCode: status, title: status == 403 ? "Forbidden" : "Licensing request rejected", detail: detail,
        type: "urn:flexicore:problem:" + slug,
        extensions: reason is null ? null : new Dictionary<string, object?> { ["reason"] = reason });
}
