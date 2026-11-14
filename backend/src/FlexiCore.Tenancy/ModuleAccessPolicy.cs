namespace FlexiCore.Tenancy;

public sealed record ModuleAccessDecision(bool Allowed, string? Reason = null, int? MaxUsers = null, int? MaxCompanies = null);

/// <summary>Host-supplied entitlement check used by tenant-scoped business endpoints.</summary>
public interface IModuleAccessPolicy
{
    Task<ModuleAccessDecision> CheckAsync(Guid userId, Guid organizationId, string module, string feature,
        string? correlationId, CancellationToken cancellationToken);

    Task<ModuleAccessDecision> CheckWithinAsync(TenantScope scope, Guid userId, Guid organizationId,
        string module, string feature, string? correlationId, CancellationToken cancellationToken);
}

internal sealed class DenyUnconfiguredModuleAccessPolicy : IModuleAccessPolicy
{
    public Task<ModuleAccessDecision> CheckAsync(Guid userId, Guid organizationId, string module, string feature,
        string? correlationId, CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleAccessDecision(false, "entitlement_service_unavailable"));

    public Task<ModuleAccessDecision> CheckWithinAsync(TenantScope scope, Guid userId, Guid organizationId,
        string module, string feature, string? correlationId, CancellationToken cancellationToken) =>
        Task.FromResult(new ModuleAccessDecision(false, "entitlement_service_unavailable"));
}
