using System.Security.Claims;
using FlexiCore.Devices.Data;
using FlexiCore.Devices.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace FlexiCore.Devices;

/// <summary>
/// Local mode only. Pairing is anonymous (license required, one-time code, rate limited); everything else needs
/// a paired device. The device secret is returned exactly once, in the pairing response.
/// </summary>
public static class DeviceEndpoints
{
    private const int MaxDeviceNameLength = 80;

    public sealed record PairRequest(string? Code, string? DeviceName);

    public sealed record PairResponse(Guid DeviceId, bool IsOwner, string Credential);

    public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/devices");

        group.MapPost("/pair", PairAsync)
            .AllowAnonymous()
            .RequireRateLimiting(DevicesModule.PairRateLimitPolicy);
        group.MapGet("/me", Me).RequireAuthorization(DevicesModule.AnyDevicePolicy);
        group.MapGet("", ListAsync).RequireAuthorization(DevicesModule.OwnerDevicePolicy);
        group.MapPost("/pairing-codes", CreatePairingCodeAsync).RequireAuthorization(DevicesModule.OwnerDevicePolicy);
        group.MapPost("/{id:guid}/revoke", RevokeAsync).RequireAuthorization(DevicesModule.OwnerDevicePolicy);

        return endpoints;
    }

    private static async Task<IResult> PairAsync(
        PairRequest request, HttpContext http, IDeviceActivationGate gate, DeviceStore store,
        TimeProvider time, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (!await gate.IsActivatedAsync(ct))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "A valid license is required",
                type: "urn:flexicore:problem:license-required");
        }

        var name = request.DeviceName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxDeviceNameLength)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "The device name must be 1 to 80 characters",
                type: "urn:flexicore:problem:invalid-device-name");
        }

        // The same answer for malformed, unknown, used and expired codes, so nothing can be probed.
        if (!PairingCode.TryNormalize(request.Code, out var normalized))
            return InvalidCode();

        var result = await store.PairAsync(normalized, name, time.GetUtcNow(), http.TraceIdentifier, ct);
        if (result.Outcome != PairOutcome.Paired) return InvalidCode();

        return Results.Ok(new PairResponse(
            result.DeviceId, result.IsOwner, DeviceSecret.FormatCredential(result.DeviceId, result.Secret!)));
    }

    private static IResult Me(ClaimsPrincipal user, HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new
        {
            deviceId = user.FindFirstValue(DeviceClaims.DeviceId),
            name = user.FindFirstValue(DeviceClaims.DeviceName),
            isOwner = user.FindFirstValue(DeviceClaims.DeviceOwner) == "true",
        });
    }

    private static async Task<IResult> ListAsync(DeviceStore store, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await store.ListAsync(ct));
    }

    private static async Task<IResult> CreatePairingCodeAsync(
        ClaimsPrincipal user, HttpContext http, DeviceStore store, TimeProvider time, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (!Guid.TryParse(user.FindFirstValue(DeviceClaims.DeviceId), out var deviceId))
            return Results.Unauthorized();

        // Owner-capable codes are only created by the administrator command, never through the API.
        var result = await store.CreatePairingCodeAsync(
            isOwner: false, deviceId, DeviceClaims.ActorKindDevice, time.GetUtcNow(),
            DevicesModule.DevicePairingCodeLifetime, http.TraceIdentifier, ct);

        if (result.Outcome != CodeOutcome.Created)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many unused pairing codes exist",
                type: "urn:flexicore:problem:too-many-pairing-codes");
        }

        return Results.Ok(new { code = PairingCode.Format(result.Code!), expiresAt = result.ExpiresAt });
    }

    private static async Task<IResult> RevokeAsync(
        Guid id, ClaimsPrincipal user, HttpContext http, DeviceStore store, TimeProvider time, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (!Guid.TryParse(user.FindFirstValue(DeviceClaims.DeviceId), out var actorDeviceId))
            return Results.Unauthorized();

        var outcome = await store.RevokeAsync(id, actorDeviceId, time.GetUtcNow(), http.TraceIdentifier, ct);
        return outcome switch
        {
            RevokeOutcome.Revoked => Results.NoContent(),
            RevokeOutcome.NotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "The device was not found",
                type: "urn:flexicore:problem:device-not-found"),
            RevokeOutcome.AlreadyRevoked => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The device is already revoked",
                type: "urn:flexicore:problem:device-already-revoked"),
            RevokeOutcome.LastOwner => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The last active owner device cannot be revoked",
                type: "urn:flexicore:problem:last-owner-device"),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }
    private static IResult InvalidCode() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "The pairing code is invalid or has expired",
            type: "urn:flexicore:problem:invalid-pairing-code");
}