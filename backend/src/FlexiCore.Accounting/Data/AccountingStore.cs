using System.Globalization;
using System.Text.Json;
using FlexiCore.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Accounting.Data;

public enum LedgerOutcome { Ok, NotFound, Forbidden, Conflict, Invalid, Unauthenticated }

public readonly record struct LedgerResult(LedgerOutcome Outcome, Guid? Id = null, string? Message = null);

public sealed record AccountDto(
    Guid Id, string Code, string Name, string AccountType, Guid? ParentId, bool IsPostable, string Status);

public sealed record NewJournalLine(Guid AccountId, decimal Debit, decimal Credit, string? Description);

public sealed record JournalEntrySummary(
    Guid Id, long EntryNumber, DateOnly EntryDate, string Memo, Guid? ReversesEntryId,
    DateTimeOffset PostedAt, decimal Amount);

public sealed record JournalLineDto(
    int LineNo, Guid AccountId, string AccountCode, string AccountName,
    decimal Debit, decimal Credit, string? Description);

public sealed record JournalEntryDetail(JournalEntrySummary Entry, IReadOnlyList<JournalLineDto> Lines);

public sealed record TrialBalanceRow(
    Guid AccountId, string Code, string Name, string AccountType, decimal TotalDebit, decimal TotalCredit);

/// <summary>
/// Ledger reads and writes. Every call runs inside a TenantScope, so row-level security sees the
/// caller and the selected company. Writes go through the SECURITY DEFINER functions from migration
/// 0007; the app role has no direct write access. Reads return null when the company is not visible
/// to the caller, which the endpoints turn into 404.
/// </summary>
public sealed class AccountingStore(ITenantConnectionFactory factory)
{
    public async Task<IReadOnlyList<AccountDto>?> ListAccountsAsync(
        Guid userId, Guid companyId, CancellationToken ct)
    {
        await using var scope = await factory.OpenAsync(TenantContext.For(userId, companyId), ct);
        if (!await CompanyVisibleAsync(scope, companyId, ct)) return null;

        await using var cmd = new NpgsqlCommand(
            "SELECT id, code, name, account_type, parent_id, is_postable, status " +
            "FROM accounting.accounts WHERE company_id = @c ORDER BY code",
            scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("c", companyId);

        var result = new List<AccountDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new AccountDto(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetBoolean(5), reader.GetString(6)));
        }
        return result;
    }

    public async Task<IReadOnlyList<JournalEntrySummary>?> ListEntriesAsync(
        Guid userId, Guid companyId, int limit, int offset, CancellationToken ct)
    {
        await using var scope = await factory.OpenAsync(TenantContext.For(userId, companyId), ct);
        if (!await CompanyVisibleAsync(scope, companyId, ct)) return null;

        await using var cmd = new NpgsqlCommand(
            "SELECT e.id, e.entry_number, e.entry_date, e.memo, e.reverses_entry_id, e.posted_at, " +
            "       coalesce(sum(l.debit), 0) " +
            "FROM accounting.journal_entries e " +
            "JOIN accounting.journal_lines l ON l.entry_id = e.id " +
            "WHERE e.company_id = @c " +
            "GROUP BY e.id " +
            "ORDER BY e.entry_number DESC LIMIT @limit OFFSET @offset",
            scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("c", companyId);
        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("offset", offset);

        var result = new List<JournalEntrySummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(ReadSummary(reader));
        return result;
    }

    public async Task<JournalEntryDetail?> GetEntryAsync(
        Guid userId, Guid companyId, Guid entryId, CancellationToken ct)
    {
        await using var scope = await factory.OpenAsync(TenantContext.For(userId, companyId), ct);
        if (!await CompanyVisibleAsync(scope, companyId, ct)) return null;

        JournalEntrySummary? summary;
        await using (var cmd = new NpgsqlCommand(
            "SELECT e.id, e.entry_number, e.entry_date, e.memo, e.reverses_entry_id, e.posted_at, " +
            "       coalesce(sum(l.debit), 0) " +
            "FROM accounting.journal_entries e " +
            "JOIN accounting.journal_lines l ON l.entry_id = e.id " +
            "WHERE e.company_id = @c AND e.id = @id GROUP BY e.id",
            scope.Connection, scope.Transaction))
        {
            cmd.Parameters.AddWithValue("c", companyId);
            cmd.Parameters.AddWithValue("id", entryId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            summary = await reader.ReadAsync(ct) ? ReadSummary(reader) : null;
        }
        if (summary is null) return null;

        var lines = new List<JournalLineDto>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT l.line_no, l.account_id, a.code, a.name, l.debit, l.credit, l.description " +
            "FROM accounting.journal_lines l " +
            "JOIN accounting.accounts a ON a.id = l.account_id AND a.company_id = l.company_id " +
            "WHERE l.company_id = @c AND l.entry_id = @id ORDER BY l.line_no",
            scope.Connection, scope.Transaction))
        {
            cmd.Parameters.AddWithValue("c", companyId);
            cmd.Parameters.AddWithValue("id", entryId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                lines.Add(new JournalLineDto(
                    reader.GetInt32(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
                    reader.GetDecimal(4), reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
            }
        }
        return new JournalEntryDetail(summary, lines);
    }

    public async Task<IReadOnlyList<TrialBalanceRow>?> TrialBalanceAsync(
        Guid userId, Guid companyId, DateOnly? asOf, CancellationToken ct)
    {
        await using var scope = await factory.OpenAsync(TenantContext.For(userId, companyId), ct);
        if (!await CompanyVisibleAsync(scope, companyId, ct)) return null;

        await using var cmd = new NpgsqlCommand(
            "SELECT account_id, account_code, account_name, account_type, total_debit, total_credit " +
            "FROM accounting.trial_balance(@as_of)",
            scope.Connection, scope.Transaction);
        cmd.Parameters.Add(new NpgsqlParameter("as_of", NpgsqlDbType.Date)
        {
            Value = asOf.HasValue ? asOf.Value : DBNull.Value
        });

        var result = new List<TrialBalanceRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new TrialBalanceRow(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetDecimal(4), reader.GetDecimal(5)));
        }
        return result;
    }

    public Task<LedgerResult> CreateAccountAsync(
        Guid userId, Guid companyId, string code, string name, string accountType, Guid? parentId,
        bool isPostable, string? correlationId, CancellationToken ct) =>
        WriteAsync(userId, companyId, correlationId,
            "SELECT accounting.create_account(@code, @name, @type, @parent, @postable)",
            command =>
            {
                command.Parameters.AddWithValue("code", code);
                command.Parameters.AddWithValue("name", name);
                command.Parameters.AddWithValue("type", accountType);
                command.Parameters.Add(new NpgsqlParameter("parent", NpgsqlDbType.Uuid)
                {
                    Value = parentId.HasValue ? parentId.Value : DBNull.Value
                });
                command.Parameters.AddWithValue("postable", isPostable);
            }, notFoundOnForeignKey: false, ct);

    public Task<LedgerResult> PostEntryAsync(
        Guid userId, Guid companyId, DateOnly entryDate, string memo, IReadOnlyList<NewJournalLine> lines,
        string? idempotencyKey, string? correlationId, CancellationToken ct)
    {
        // Amounts travel as invariant-culture strings so no binary floating point is ever involved.
        var json = JsonSerializer.Serialize(lines.Select(l => new
        {
            accountId = l.AccountId,
            debit = l.Debit.ToString(CultureInfo.InvariantCulture),
            credit = l.Credit.ToString(CultureInfo.InvariantCulture),
            description = l.Description
        }));

        return WriteAsync(userId, companyId, correlationId,
            "SELECT accounting.post_journal_entry(@date, @memo, @lines, @key)",
            command =>
            {
                command.Parameters.Add(new NpgsqlParameter("date", NpgsqlDbType.Date) { Value = entryDate });
                command.Parameters.AddWithValue("memo", memo);
                command.Parameters.Add(new NpgsqlParameter("lines", NpgsqlDbType.Jsonb) { Value = json });
                command.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Text)
                {
                    Value = idempotencyKey is null ? DBNull.Value : idempotencyKey
                });
            }, notFoundOnForeignKey: false, ct);
    }

    public Task<LedgerResult> ReverseEntryAsync(
        Guid userId, Guid companyId, Guid entryId, DateOnly entryDate, string? idempotencyKey,
        string? correlationId, CancellationToken ct) =>
        WriteAsync(userId, companyId, correlationId,
            "SELECT accounting.reverse_journal_entry(@id, @date, @key)",
            command =>
            {
                command.Parameters.AddWithValue("id", entryId);
                command.Parameters.Add(new NpgsqlParameter("date", NpgsqlDbType.Date) { Value = entryDate });
                command.Parameters.Add(new NpgsqlParameter("key", NpgsqlDbType.Text)
                {
                    Value = idempotencyKey is null ? DBNull.Value : idempotencyKey
                });
            }, notFoundOnForeignKey: true, ct);

    private async Task<LedgerResult> WriteAsync(
        Guid userId, Guid companyId, string? correlationId, string sql, Action<NpgsqlCommand> bind,
        bool notFoundOnForeignKey, CancellationToken ct)
    {
        try
        {
            await using var scope = await factory.OpenAsync(
                TenantContext.For(userId, companyId, correlationId), ct);
            await using var cmd = new NpgsqlCommand(sql, scope.Connection, scope.Transaction);
            bind(cmd);
            var id = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            // The deferred balance trigger fires at commit, so a failure can surface here.
            await scope.CommitAsync(ct);
            return new LedgerResult(LedgerOutcome.Ok, id);
        }
        catch (PostgresException ex)
        {
            return Map(ex, notFoundOnForeignKey);
        }
    }

    private static LedgerResult Map(PostgresException ex, bool notFoundOnForeignKey) => ex.SqlState switch
    {
        "42501" => new LedgerResult(LedgerOutcome.Forbidden),
        "28000" => new LedgerResult(LedgerOutcome.Unauthenticated),
        "23505" => new LedgerResult(LedgerOutcome.Conflict),
        "23503" when notFoundOnForeignKey => new LedgerResult(LedgerOutcome.NotFound),
        "23503" or "22P02" or "22003" or "22007" => new LedgerResult(
            LedgerOutcome.Invalid, Message: "One or more values are not valid."),
        "23514" => new LedgerResult(
            LedgerOutcome.Invalid,
            Message: ex.ConstraintName is null ? ex.MessageText : "One or more values are not valid."),
        _ => throw ex
    };

    private static async Task<bool> CompanyVisibleAsync(TenantScope scope, Guid companyId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM tenancy.companies WHERE id = @id", scope.Connection, scope.Transaction);
        cmd.Parameters.AddWithValue("id", companyId);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private static JournalEntrySummary ReadSummary(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetInt64(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetFieldValue<DateTimeOffset>(5),
        reader.GetDecimal(6));
}
