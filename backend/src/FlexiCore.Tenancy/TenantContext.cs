namespace FlexiCore.Tenancy;

/// <summary>
/// The identity a database scope runs as. UserId drives row-level security;
/// CompanyId selects the company for company-scoped tables (null = none selected).
/// CorrelationId is recorded in audit rows written inside the scope.
/// </summary>
public sealed record TenantContext(Guid UserId, Guid? CompanyId = null, string? CorrelationId = null)
{
    public static TenantContext For(Guid userId, Guid? companyId = null, string? correlationId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user id is required.", nameof(userId));
        }

        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company id cannot be an empty GUID.", nameof(companyId));
        }

        return new TenantContext(userId, companyId, correlationId);
    }
}