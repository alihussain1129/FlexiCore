using System.Security.Claims;
using System.Text.RegularExpressions;
using FlexiCore.Accounting.Data;
using FlexiCore.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace FlexiCore.Accounting.Endpoints;

public sealed record CreateAccountRequest(
    string? Code, string? Name, string? AccountType, Guid? ParentId, bool? IsPostable);

public sealed record JournalLineRequest(Guid? AccountId, decimal? Debit, decimal? Credit, string? Description);

public sealed record PostJournalEntryRequest(DateOnly? EntryDate, string? Memo, List<JournalLineRequest>? Lines);

public sealed record ReverseJournalEntryRequest(DateOnly? EntryDate);

internal static class AccountingEndpoints
{
    // These mirror the CHECK constraints in migration 0007 so callers get a clear 400.
    private static readonly Regex CodePattern =
        new(@"^[A-Za-z0-9][A-Za-z0-9.-]{0,31}\z", RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AccountTypes =
        ["asset", "liability", "equity", "revenue", "expense"];

    private const int MaxListPage = 100;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/companies/{companyId:guid}/accounting").RequireAuthorization();

        group.MapGet("/accounts", ListAccountsAsync);
        group.MapPost("/accounts", CreateAccountAsync).RequireRateLimiting(TenancyModule.CreateRateLimitPolicy);
        group.MapGet("/journal-entries", ListEntriesAsync);
        group.MapGet("/journal-entries/{entryId:guid}", GetEntryAsync);
        group.MapPost("/journal-entries", PostEntryAsync).RequireRateLimiting(TenancyModule.CreateRateLimitPolicy);
        group.MapPost("/journal-entries/{entryId:guid}/reverse", ReverseEntryAsync)
            .RequireRateLimiting(TenancyModule.CreateRateLimitPolicy);
        group.MapGet("/trial-balance", TrialBalanceAsync);
    }

    private static async Task<IResult> ListAccountsAsync(
        Guid companyId, ClaimsPrincipal principal, AccountingStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        var accounts = await store.ListAccountsAsync(userId, companyId, ct);
        return accounts is null ? Results.NotFound() : Results.Ok(accounts);
    }

    private static async Task<IResult> CreateAccountAsync(
        Guid companyId, CreateAccountRequest request, HttpContext http, ClaimsPrincipal principal,
        AccountingStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

        var code = request.Code?.Trim();
        var name = request.Name?.Trim();
        var type = request.AccountType?.Trim().ToLowerInvariant();
        var errors = new Dictionary<string, string[]>();
        if (code is null || !CodePattern.IsMatch(code))
            errors["code"] = ["Code must be 1 to 32 letters, digits, dots or dashes, starting with a letter or digit."];
        if (string.IsNullOrEmpty(name) || name.Length > 160)
            errors["name"] = ["Name must be 1 to 160 characters."];
        if (type is null || !AccountTypes.Contains(type))
            errors["accountType"] = ["Account type must be asset, liability, equity, revenue or expense."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.CreateAccountAsync(
            userId, companyId, code!, name!, type!, request.ParentId, request.IsPostable ?? true,
            http.TraceIdentifier, ct);
        return result.Outcome == LedgerOutcome.Ok
            ? Results.Json(new { id = result.Id }, statusCode: StatusCodes.Status201Created)
            : Failure(result, "An account with this code already exists.");
    }

    private static async Task<IResult> ListEntriesAsync(
        Guid companyId, int? limit, int? offset, ClaimsPrincipal principal, AccountingStore store,
        CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        var pageSize = limit ?? MaxListPage;
        var pageOffset = offset ?? 0;
        if (pageSize is < 1 or > MaxListPage || pageOffset is < 0 or > 5_000_000)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["limit"] = ["Limit must be 1 to 100 and offset must not be negative."]
            });
        var entries = await store.ListEntriesAsync(userId, companyId, pageSize, pageOffset, ct);
        return entries is null ? Results.NotFound() : Results.Ok(entries);
    }

    private static async Task<IResult> GetEntryAsync(
        Guid companyId, Guid entryId, ClaimsPrincipal principal, AccountingStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        var entry = await store.GetEntryAsync(userId, companyId, entryId, ct);
        return entry is null ? Results.NotFound() : Results.Ok(entry);
    }

    private static async Task<IResult> PostEntryAsync(
        Guid companyId, PostJournalEntryRequest request, HttpContext http, ClaimsPrincipal principal,
        AccountingStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

        var errors = new Dictionary<string, string[]>();
        var memo = request.Memo?.Trim();
        if (request.EntryDate is null) errors["entryDate"] = ["Entry date is required."];
        if (string.IsNullOrEmpty(memo) || memo.Length > 500) errors["memo"] = ["Memo must be 1 to 500 characters."];
        if (!TryGetIdempotencyKey(http, out var key)) errors["Idempotency-Key"] = ["Key must be 1 to 100 characters."];

        var lines = new List<NewJournalLine>();
        if (request.Lines is null || request.Lines.Count < 2 || request.Lines.Count > 500)
        {
            errors["lines"] = ["An entry needs between 2 and 500 lines."];
        }
        else
        {
            decimal totalDebit = 0, totalCredit = 0;
            foreach (var (line, index) in request.Lines.Select((l, i) => (l, i)))
            {
                var debit = line.Debit ?? 0m;
                var credit = line.Credit ?? 0m;
                if (line.AccountId is null || line.AccountId == Guid.Empty)
                    errors[$"lines[{index}].accountId"] = ["Account is required."];
                else if (debit < 0 || credit < 0)
                    errors[$"lines[{index}]"] = ["Amounts cannot be negative."];
                else if (debit != decimal.Round(debit, 4) || credit != decimal.Round(credit, 4))
                    errors[$"lines[{index}]"] = ["Amounts may have at most 4 decimal places."];
                else if ((debit == 0) == (credit == 0))
                    errors[$"lines[{index}]"] = ["Each line needs exactly one of debit or credit, above zero."];
                else if (line.Description is { Length: > 300 })
                    errors[$"lines[{index}].description"] = ["Description is limited to 300 characters."];
                else
                    lines.Add(new NewJournalLine(line.AccountId.Value, debit, credit, line.Description));
                totalDebit += debit;
                totalCredit += credit;
            }
            if (errors.Count == 0 && totalDebit != totalCredit)
                errors["lines"] = ["Total debits must equal total credits."];
        }
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.PostEntryAsync(
            userId, companyId, request.EntryDate!.Value, memo!, lines, key, http.TraceIdentifier, ct);
        return result.Outcome == LedgerOutcome.Ok
            ? Results.Json(new { id = result.Id }, statusCode: StatusCodes.Status201Created)
            : Failure(result, "This request conflicts with an existing entry.");
    }

    private static async Task<IResult> ReverseEntryAsync(
        Guid companyId, Guid entryId, ReverseJournalEntryRequest request, HttpContext http,
        ClaimsPrincipal principal, AccountingStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();

        var errors = new Dictionary<string, string[]>();
        if (request.EntryDate is null) errors["entryDate"] = ["Entry date is required."];
        if (!TryGetIdempotencyKey(http, out var key)) errors["Idempotency-Key"] = ["Key must be 1 to 100 characters."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.ReverseEntryAsync(
            userId, companyId, entryId, request.EntryDate!.Value, key, http.TraceIdentifier, ct);
        return result.Outcome == LedgerOutcome.Ok
            ? Results.Json(new { id = result.Id }, statusCode: StatusCodes.Status201Created)
            : Failure(result, "This entry has already been reversed.");
    }

    private static async Task<IResult> TrialBalanceAsync(
        Guid companyId, DateOnly? asOf, ClaimsPrincipal principal, AccountingStore store, CancellationToken ct)
    {
        if (!TryGetUserId(principal, out var userId)) return Results.Unauthorized();
        var rows = await store.TrialBalanceAsync(userId, companyId, asOf, ct);
        if (rows is null) return Results.NotFound();

        var totalDebit = rows.Sum(r => r.TotalDebit);
        var totalCredit = rows.Sum(r => r.TotalCredit);
        return Results.Ok(new
        {
            asOf,
            rows,
            totalDebit,
            totalCredit,
            isBalanced = totalDebit == totalCredit
        });
    }

    private static IResult Failure(LedgerResult result, string conflictMessage) => result.Outcome switch
    {
        LedgerOutcome.Forbidden => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden"),
        LedgerOutcome.Unauthenticated => Results.Unauthorized(),
        LedgerOutcome.NotFound => Results.NotFound(),
        LedgerOutcome.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: conflictMessage),
        _ => Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["request"] = [result.Message ?? "The request is not valid."]
        })
    };

    private static bool TryGetIdempotencyKey(HttpContext http, out string? key)
    {
        var value = http.Request.Headers["Idempotency-Key"].ToString().Trim();
        key = value.Length == 0 ? null : value;
        return value.Length <= 100;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var sub = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out userId) && userId != Guid.Empty;
    }
}
