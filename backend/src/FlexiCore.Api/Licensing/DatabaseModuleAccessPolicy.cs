using FlexiCore.Tenancy;
using FlexiCore.Tenancy.Data;

namespace FlexiCore.Api.Licensing;

internal sealed class DatabaseModuleAccessPolicy(
    DeploymentSettings settings, LicensingStore store, TenancyAuditWriter audit) : IModuleAccessPolicy
{
    public async Task<ModuleAccessDecision> CheckAsync(Guid userId, Guid organizationId, string module, string feature,
        string? correlationId, CancellationToken cancellationToken)
    {
        // Local installations use the installed signed license gate; hosted organizations use DB-backed entitlements.
        if (settings.Mode == DeploymentMode.Local) return new(true);
        var result = await store.CheckAccessAsync(TenantContext.For(userId, correlationId: correlationId),
            organizationId, module, feature, cancellationToken);
        return await AuditIfDeniedAsync(result, userId, organizationId, module, feature, correlationId, cancellationToken);
    }

    public async Task<ModuleAccessDecision> CheckWithinAsync(TenantScope scope, Guid userId, Guid organizationId,
        string module, string feature, string? correlationId, CancellationToken cancellationToken)
    {
        if (settings.Mode == DeploymentMode.Local) return new(true);
        var result = await store.CheckAccessAsync(scope, organizationId, module, feature, cancellationToken);
        return result.Allowed
            ? new(true, MaxUsers: result.MaxUsers, MaxCompanies: result.MaxCompanies)
            : new(false, result.Reason);
    }

    private async Task<ModuleAccessDecision> AuditIfDeniedAsync(EntitlementAccess result, Guid userId,
        Guid organizationId, string module, string feature, string? correlationId, CancellationToken cancellationToken)
    {
        if (!result.Allowed)
        {
            await audit.WriteAsync(new TenancyAuditEvent(
                "licensing.entitlement.denied", "denied", userId, "organization", organizationId.ToString("D"),
                correlationId, new Dictionary<string, string> { ["module"] = module, ["feature"] = feature,
                    ["reason"] = result.Reason ?? "not_licensed" }), cancellationToken);
            return new(false, result.Reason);
        }
        return new(true, MaxUsers: result.MaxUsers, MaxCompanies: result.MaxCompanies);
    }
}
