using System.Security.Claims;
using System.Text.Encodings.Web;
using FlexiCore.Devices.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FlexiCore.Devices.Security;

/// <summary>
/// Authenticates "Authorization: Device &lt;deviceId&gt;.&lt;secret&gt;". A revoked or unknown device and a wrong secret
/// give the same failure. The principal is a device, never a user.
/// </summary>
internal sealed class DeviceAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    DeviceStore store,
    TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!DeviceSecret.IsDeviceHeader(header)) return AuthenticateResult.NoResult();

        if (!DeviceSecret.TryParseAuthorizationHeader(header, out var deviceId, out var secret))
            return AuthenticateResult.Fail("Invalid device credential.");

        var credential = await store.FindActiveCredentialAsync(deviceId, Context.RequestAborted);
        if (credential is null || !DeviceSecret.Verify(secret, credential.SecretHash))
            return AuthenticateResult.Fail("Invalid device credential.");

        try
        {
            await store.TouchAsync(credential.Id, time.GetUtcNow(), Context.RequestAborted);
        }
        catch (NpgsqlException)
        {
            // Last-seen is informational; failing to record it must not lock a valid device out.
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(DeviceClaims.ActorKind, DeviceClaims.ActorKindDevice),
                new Claim(DeviceClaims.DeviceId, credential.Id.ToString()),
                new Claim(DeviceClaims.DeviceName, credential.Name),
                new Claim(DeviceClaims.DeviceOwner, credential.IsOwner ? "true" : "false"),
            ],
            Scheme.Name,
            nameType: DeviceClaims.DeviceName,
            roleType: null);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}