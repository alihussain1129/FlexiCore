using FlexiCore.Tenancy;

namespace FlexiCore.Catalog.Data;

/// <summary>The only place that knows how to build a company-scoped TenantContext.</summary>
internal static class CatalogTenantContext
{
    public static TenantContext Create(Guid userId, Guid companyId, string? correlationId) =>
        TenantContext.For(userId, companyId, correlationId);
}