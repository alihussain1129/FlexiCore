using Npgsql;

namespace FlexiCore.Tenancy.Data;

public enum CreateOutcome { Created, Forbidden, Conflict, Invalid, Unauthenticated, QuotaExceeded }

public readonly record struct CreateResult(CreateOutcome Outcome, Guid? Id = null);

public sealed record OrganizationSummary(Guid Id, string Name, string Slug, string Role);

public sealed record CompanySummary(
    Guid Id, Guid OrganizationId, string Name, string Slug, string IndustryKey, string Role);

/// <summary>
/// Tenancy reads and writes. Every call runs inside a TenantScope, so row-level security sees the caller.
/// Organization and company creation go through the SECURITY DEFINER functions, which audit successes
/// in the same transaction. Refusals are audited here, after the rollback.
/// </summary>
public sealed class TenancyStore
{
    private const int MaxListSize = 100;

    private const string CompanySelect =
        "SELECT c.id, c.organization_id, c.name, c.slug, c.industry_key, cm.role " +
        "FROM tenancy.companies c " +
        "JOIN tenancy.company_memberships cm ON cm.company_id = c.id " +
        "WHERE cm.user_id = @user_id AND cm.status = 'active'";

    private readonly ITenantConnectionFactory _factory;
    private readonly TenancyAuditWriter _audit;

    public TenancyStore(ITenantConnectionFactory factory, TenancyAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
    }

    private sealed record AuditTarget(string Action, string TargetType, string? TargetId, string Slug);

    public Task<CreateResult> CreateOrganizationAsync(
        Guid userId, string name, string slug, string? correlationId, CancellationToken ct) =>
        CreateAsync(userId, correlationId, "SELECT tenancy.create_organization(@name, @slug)", command =>
        {
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("slug", slug);
        }, new AuditTarget("tenancy.organization.create", "organization", null, slug), ct);

    public Task<CreateResult> CreateCompanyAsync(
        Guid userId, Guid organizationId, string name, string slug, string industryKey, string? correlationId,
        CancellationToken ct) =>
        CreateAsync(userId, correlationId, "SELECT tenancy.create_company(@organization_id, @name, @slug, @industry_key)", command =>
        {
            command.Parameters.AddWithValue("organization_id", organizationId);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("slug", slug);
            command.Parameters.AddWithValue("industry_key", industryKey);
        }, new AuditTarget("tenancy.company.create", "organization", organizationId.ToString(), slug), ct);

    public async Task<IReadOnlyList<OrganizationSummary>> ListOrganizationsAsync(Guid userId, CancellationToken ct)
    {
        await using var scope = await _factory.OpenAsync(TenantContext.For(userId), ct);
        await using var command = new NpgsqlCommand(
            "SELECT o.id, o.name, o.slug, om.role " +
            "FROM tenancy.organizations o " +
            "JOIN tenancy.organization_memberships om ON om.organization_id = o.id " +
            "WHERE om.user_id = @user_id AND om.status = 'active' " +
            "ORDER BY o.name, o.id LIMIT @limit",
            scope.Connection, scope.Transaction);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("limit", MaxListSize);

        var list = new List<OrganizationSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new OrganizationSummary(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return list;
    }

    public async Task<IReadOnlyList<CompanySummary>> ListCompaniesAsync(Guid userId, CancellationToken ct)
    {
        await using var scope = await _factory.OpenAsync(TenantContext.For(userId), ct);
        await using var command = new NpgsqlCommand(
            CompanySelect + " ORDER BY c.name, c.id LIMIT @limit", scope.Connection, scope.Transaction);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("limit", MaxListSize);
        return await ReadCompaniesAsync(command, ct);
    }

    /// <summary>Null when the company does not exist or the caller is not an active member (the two cases are indistinguishable).</summary>
    public async Task<CompanySummary?> GetCompanyAsync(Guid userId, Guid companyId, CancellationToken ct)
    {
        await using var scope = await _factory.OpenAsync(TenantContext.For(userId), ct);
        await using var command = new NpgsqlCommand(
            CompanySelect + " AND c.id = @company_id", scope.Connection, scope.Transaction);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("company_id", companyId);
        var companies = await ReadCompaniesAsync(command, ct);
        return companies.Count == 0 ? null : companies[0];
    }

    private async Task<CreateResult> CreateAsync(
        Guid userId, string? correlationId, string sql, Action<NpgsqlCommand> bind, AuditTarget target,
        CancellationToken ct)
    {
        CreateOutcome refused;
        try
        {
            await using var scope = await _factory.OpenAsync(
                TenantContext.For(userId, correlationId: correlationId), ct);
            await using var command = new NpgsqlCommand(sql, scope.Connection, scope.Transaction);
            bind(command);
            var id = (Guid)(await command.ExecuteScalarAsync(ct))!;
            await scope.CommitAsync(ct);
            return new CreateResult(CreateOutcome.Created, id);
        }
        catch (PostgresException ex) when (Classify(ex) is { } outcome)
        {
            refused = outcome;
        }

        // The scope was disposed without committing, so nothing was created. Record the refusal on its own.
        await _audit.WriteAsync(new TenancyAuditEvent(
            target.Action,
            AuditOutcome(refused),
            userId,
            target.TargetType,
            target.TargetId,
            correlationId,
            new Dictionary<string, string> { ["slug"] = target.Slug, ["reason"] = Reason(refused) }), ct);

        return new CreateResult(refused);
    }

    private static CreateOutcome? Classify(PostgresException ex) => ex.SqlState switch
    {
        PostgresErrorCodes.InsufficientPrivilege => CreateOutcome.Forbidden,
        PostgresErrorCodes.UniqueViolation => CreateOutcome.Conflict,
        PostgresErrorCodes.CheckViolation => CreateOutcome.Invalid,
        PostgresErrorCodes.InvalidAuthorizationSpecification => CreateOutcome.Unauthenticated,
        "53400" => CreateOutcome.QuotaExceeded, // configuration_limit_exceeded, raised by the organization cap
        _ => null
    };

    // The audit table only accepts success, failure and denied.
    private static string AuditOutcome(CreateOutcome outcome) => outcome switch
    {
        CreateOutcome.Forbidden or CreateOutcome.QuotaExceeded or CreateOutcome.Unauthenticated => "denied",
        _ => "failure"
    };

    private static string Reason(CreateOutcome outcome) => outcome switch
    {
        CreateOutcome.Forbidden => "not_permitted",
        CreateOutcome.QuotaExceeded => "limit_reached",
        CreateOutcome.Unauthenticated => "no_active_user",
        CreateOutcome.Conflict => "duplicate_slug",
        _ => "invalid_input"
    };

    private static async Task<List<CompanySummary>> ReadCompaniesAsync(NpgsqlCommand command, CancellationToken ct)
    {
        var list = new List<CompanySummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new CompanySummary(
                reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        }

        return list;
    }
}