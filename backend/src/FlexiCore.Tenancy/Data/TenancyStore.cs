using Npgsql;

namespace FlexiCore.Tenancy.Data;

public enum CreateOutcome { Created, Forbidden, Conflict, Invalid, Unauthenticated }

public readonly record struct CreateResult(CreateOutcome Outcome, Guid? Id = null);

public sealed record OrganizationSummary(Guid Id, string Name, string Slug, string Role);

public sealed record CompanySummary(
    Guid Id, Guid OrganizationId, string Name, string Slug, string IndustryKey, string Role);

/// <summary>
/// Tenancy reads and writes. Every call runs inside a TenantScope, so row-level security sees the caller.
/// Organization and company creation go through the SECURITY DEFINER functions from migration 0005.
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

    public TenancyStore(ITenantConnectionFactory factory)
    {
        _factory = factory;
    }

    public Task<CreateResult> CreateOrganizationAsync(Guid userId, string name, string slug, CancellationToken ct) =>
        CreateAsync(userId, "SELECT tenancy.create_organization(@name, @slug)", command =>
        {
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("slug", slug);
        }, ct);

    public Task<CreateResult> CreateCompanyAsync(
        Guid userId, Guid organizationId, string name, string slug, string industryKey, CancellationToken ct) =>
        CreateAsync(userId, "SELECT tenancy.create_company(@organization_id, @name, @slug, @industry_key)", command =>
        {
            command.Parameters.AddWithValue("organization_id", organizationId);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("slug", slug);
            command.Parameters.AddWithValue("industry_key", industryKey);
        }, ct);

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
        Guid userId, string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        try
        {
            await using var scope = await _factory.OpenAsync(TenantContext.For(userId), ct);
            await using var command = new NpgsqlCommand(sql, scope.Connection, scope.Transaction);
            bind(command);
            var id = (Guid)(await command.ExecuteScalarAsync(ct))!;
            await scope.CommitAsync(ct);
            return new CreateResult(CreateOutcome.Created, id);
        }
        catch (PostgresException ex) when (Classify(ex) is { } outcome)
        {
            // The scope was disposed without committing, so the transaction is rolled back.
            return new CreateResult(outcome);
        }
    }

    private static CreateOutcome? Classify(PostgresException ex) => ex.SqlState switch
    {
        PostgresErrorCodes.InsufficientPrivilege => CreateOutcome.Forbidden,
        PostgresErrorCodes.UniqueViolation => CreateOutcome.Conflict,
        PostgresErrorCodes.CheckViolation => CreateOutcome.Invalid,
        PostgresErrorCodes.InvalidAuthorizationSpecification => CreateOutcome.Unauthenticated,
        _ => null
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