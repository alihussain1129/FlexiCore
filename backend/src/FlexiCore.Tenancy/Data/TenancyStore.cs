using Npgsql;

namespace FlexiCore.Tenancy.Data;

public enum CreateOutcome { Created, Forbidden, Conflict, Invalid, Unauthenticated, QuotaExceeded, LastOwner }

public readonly record struct CreateResult(CreateOutcome Outcome, Guid? Id = null);

public sealed record OrganizationSummary(Guid Id, string Name, string Slug, string Role);

public sealed record MemberSummary(Guid UserId, string? Email, string Role, string Status, DateTimeOffset JoinedAt, string? Username = null);

public sealed record CompanySummary(
    Guid Id, Guid OrganizationId, string Name, string Slug, string IndustryKey, string Role);

/// <summary>
/// Tenancy reads and writes. Every call runs inside a TenantScope, so row-level security sees the caller.
/// Organization and company creation go through the SECURITY DEFINER functions, which audit successes
/// in the same transaction. Refusals are audited here, after the rollback.
/// </summary>
public sealed class TenancyStore
{
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

    private sealed record AuditTarget(
        string Action, string TargetType, string? TargetId, string Slug,
        string MetadataKey = "slug", string ConflictReason = "duplicate_slug");

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

    public Task<CreateResult> AddMemberAsync(
        Guid userId, Guid organizationId, string normalizedEmail, string role, string? correlationId, CancellationToken ct) =>
        CreateAsync(userId, correlationId, "SELECT tenancy.add_organization_member(@organization_id, @email, @role)", command =>
        {
            command.Parameters.AddWithValue("organization_id", organizationId);
            command.Parameters.AddWithValue("email", normalizedEmail);
            command.Parameters.AddWithValue("role", role);
        }, new AuditTarget("tenancy.member.add", "organization", organizationId.ToString(), role,
            MetadataKey: "role", ConflictReason: "already_member"), ct);

    public Task<CreateResult> ChangeMemberRoleAsync(
        Guid userId, Guid organizationId, Guid targetUserId, string role, string? correlationId, CancellationToken ct) =>
        CreateAsync(userId, correlationId, "SELECT tenancy.change_organization_member_role(@organization_id, @target, @role)", command =>
        {
            command.Parameters.AddWithValue("organization_id", organizationId);
            command.Parameters.AddWithValue("target", targetUserId);
            command.Parameters.AddWithValue("role", role);
        }, new AuditTarget("tenancy.member.role_change", "organization", organizationId.ToString(), role, MetadataKey: "role"), ct);

    public Task<CreateResult> RemoveMemberAsync(
        Guid userId, Guid organizationId, Guid targetUserId, string? correlationId, CancellationToken ct) =>
        CreateAsync(userId, correlationId, "SELECT tenancy.remove_organization_member(@organization_id, @target)", command =>
        {
            command.Parameters.AddWithValue("organization_id", organizationId);
            command.Parameters.AddWithValue("target", targetUserId);
        }, new AuditTarget("tenancy.member.remove", "organization", organizationId.ToString(), targetUserId.ToString(), MetadataKey: "member_user_id"), ct);

    /// <summary>Null when the caller may not list this organization (not a member, or not an owner or admin).</summary>
    public async Task<IReadOnlyList<MemberSummary>?> ListMembersAsync(Guid userId, Guid organizationId, CancellationToken ct)
    {
        try
        {
            await using var scope = await _factory.OpenAsync(TenantContext.For(userId), ct);
            await using var command = new NpgsqlCommand(
                "SELECT member_user_id, member_email, member_role, member_status, joined_at, member_username " +
                "FROM tenancy.list_organization_members(@organization_id)", scope.Connection, scope.Transaction);
            command.Parameters.AddWithValue("organization_id", organizationId);

            var list = new List<MemberSummary>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(new MemberSummary(
                    reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetFieldValue<DateTimeOffset>(4), reader.IsDBNull(5) ? null : reader.GetString(5)));
            }

            return list;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InsufficientPrivilege)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<OrganizationSummary>> ListOrganizationsAsync(
        Guid userId, int limit, int offset, CancellationToken ct)
    {
        await using var scope = await _factory.OpenAsync(TenantContext.For(userId), ct);
        await using var command = new NpgsqlCommand(
            "SELECT o.id, o.name, o.slug, om.role " +
            "FROM tenancy.organizations o " +
            "JOIN tenancy.organization_memberships om ON om.organization_id = o.id " +
            "WHERE om.user_id = @user_id AND om.status = 'active' " +
            "ORDER BY o.name, o.id LIMIT @limit OFFSET @offset",
            scope.Connection, scope.Transaction);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("limit", limit);
        command.Parameters.AddWithValue("offset", offset);

        var list = new List<OrganizationSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new OrganizationSummary(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return list;
    }

    public async Task<IReadOnlyList<CompanySummary>> ListCompaniesAsync(
        Guid userId, int limit, int offset, CancellationToken ct)
    {
        await using var scope = await _factory.OpenAsync(TenantContext.For(userId), ct);
        await using var command = new NpgsqlCommand(
            CompanySelect + " ORDER BY c.name, c.id LIMIT @limit OFFSET @offset", scope.Connection, scope.Transaction);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("limit", limit);
        command.Parameters.AddWithValue("offset", offset);
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
            new Dictionary<string, string>
            {
                [target.MetadataKey] = target.Slug,
                ["reason"] = refused == CreateOutcome.Conflict ? target.ConflictReason : Reason(refused)
            }), ct);

        return new CreateResult(refused);
    }

    private static CreateOutcome? Classify(PostgresException ex) => ex.SqlState switch
    {
        PostgresErrorCodes.InsufficientPrivilege => CreateOutcome.Forbidden,
        PostgresErrorCodes.UniqueViolation => CreateOutcome.Conflict,
        PostgresErrorCodes.CheckViolation => CreateOutcome.Invalid,
        PostgresErrorCodes.InvalidAuthorizationSpecification => CreateOutcome.Unauthenticated,
        "53400" => CreateOutcome.QuotaExceeded, // configuration_limit_exceeded, raised by the organization cap
        "55000" => CreateOutcome.LastOwner, // object_not_in_prerequisite_state, raised by the last-owner guard
        _ => null
    };

    // The audit table only accepts success, failure and denied.
    private static string AuditOutcome(CreateOutcome outcome) => outcome switch
    {
        CreateOutcome.Forbidden or CreateOutcome.QuotaExceeded or CreateOutcome.Unauthenticated or CreateOutcome.LastOwner => "denied",
        _ => "failure"
    };

    private static string Reason(CreateOutcome outcome) => outcome switch
    {
        CreateOutcome.Forbidden => "not_permitted",
        CreateOutcome.QuotaExceeded => "limit_reached",
        CreateOutcome.LastOwner => "last_owner",
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