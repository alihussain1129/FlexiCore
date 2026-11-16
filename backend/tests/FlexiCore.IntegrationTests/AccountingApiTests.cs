using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlexiCore.IntegrationTests.Infrastructure;
using Npgsql;

namespace FlexiCore.IntegrationTests;

[Collection("postgres")]
public sealed class AccountingApiTests(PostgresFixture postgres)
{
    private const string Password = Scenario.Password;
    private const string OwnerEmail = "ledger.owner@example.test";
    private const string MemberEmail = "ledger.member@example.test";
    private const string OutsiderEmail = "ledger.outsider@example.test";
    private const string EntryDate = "2026-11-16";

    private sealed record Ctx(
        string Owner, string Member, string Outsider, Guid OrgId, Guid CompanyId, Guid Cash, Guid Revenue);

    private static async Task<string> SignUpAsync(IdentityTestApp app, string email)
    {
        var register = await app.RegisterAsync(email, Password);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        return (await Scenario.LoginAsync(app, email, Password)).AccessToken;
    }

    private static Task<HttpResponseMessage> SendAsync(
        IdentityTestApp app, HttpMethod method, string url, string token, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        return app.Client.SendAsync(request);
    }

    private static async Task ExecAsync(IdentityTestApp app, string sql)
    {
        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(IdentityTestApp app, string sql)
    {
        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> CreatedIdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await IdentityTestApp.ReadJsonAsync(response)).GetProperty("id").GetGuid();
    }

    private static async Task<Ctx> SetUpAsync(IdentityTestApp app)
    {
        var owner = await SignUpAsync(app, OwnerEmail);
        var member = await SignUpAsync(app, MemberEmail);
        var outsider = await SignUpAsync(app, OutsiderEmail);

        var orgId = await CreatedIdAsync(await SendAsync(app, HttpMethod.Post, "/api/v1/organizations", owner,
            new { name = "Ledger Org", slug = "ledger-org" }));
        var companyId = await CreatedIdAsync(await SendAsync(app, HttpMethod.Post,
            $"/api/v1/organizations/{orgId}/companies", owner,
            new { name = "Ledger Co", slug = "ledger-co", industryKey = "retail" }));

        // The member belongs to the company with the plain 'member' role (read-only for the ledger).
        await ExecAsync(app, $"""
            INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
            SELECT '{orgId}', id, 'member' FROM identity.users
             WHERE lower(email_normalized) = lower('{MemberEmail}');
            INSERT INTO tenancy.company_memberships (company_id, organization_id, user_id, role)
            SELECT '{companyId}', '{orgId}', id, 'member' FROM identity.users
             WHERE lower(email_normalized) = lower('{MemberEmail}');
            """);

        var cash = await CreatedIdAsync(await SendAsync(app, HttpMethod.Post,
            $"/api/v1/companies/{companyId}/accounting/accounts", owner,
            new { code = "1000", name = "Cash", accountType = "asset" }));
        var revenue = await CreatedIdAsync(await SendAsync(app, HttpMethod.Post,
            $"/api/v1/companies/{companyId}/accounting/accounts", owner,
            new { code = "4000", name = "Sales revenue", accountType = "revenue" }));

        return new Ctx(owner, member, outsider, orgId, companyId, cash, revenue);
    }

    private static Task<HttpResponseMessage> PostEntryAsync(
        IdentityTestApp app, Ctx c, string token, decimal debit, decimal credit, string? key = null) =>
        SendAsync(app, HttpMethod.Post, $"/api/v1/companies/{c.CompanyId}/accounting/journal-entries", token,
            new
            {
                entryDate = EntryDate,
                memo = "Cash sale",
                lines = new object[]
                {
                    new { accountId = c.Cash, debit, credit = 0m },
                    new { accountId = c.Revenue, debit = 0m, credit }
                }
            }, key);

    private static async Task<JsonElement> TrialBalanceAsync(IdentityTestApp app, Ctx c)
    {
        var response = await SendAsync(app, HttpMethod.Get,
            $"/api/v1/companies/{c.CompanyId}/accounting/trial-balance", c.Owner);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await IdentityTestApp.ReadJsonAsync(response);
    }

    [Fact]
    public async Task Balanced_entry_is_persisted_and_trial_balance_balances()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);

        await CreatedIdAsync(await PostEntryAsync(app, c, c.Owner, 100.25m, 100.25m));

        var tb = await TrialBalanceAsync(app, c);
        Assert.True(tb.GetProperty("isBalanced").GetBoolean());
        Assert.Equal(100.25m, tb.GetProperty("totalDebit").GetDecimal());
        Assert.Equal(100.25m, tb.GetProperty("totalCredit").GetDecimal());
        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM accounting.journal_entries"));
        Assert.Equal(2, await CountAsync(app, "SELECT count(*) FROM accounting.journal_lines"));
    }

    [Fact]
    public async Task Unbalanced_entry_is_rejected_and_nothing_is_stored()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);

        var response = await PostEntryAsync(app, c, c.Owner, 100m, 90m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(app, "SELECT count(*) FROM accounting.journal_entries"));
    }

    [Fact]
    public async Task Amounts_with_more_than_four_decimals_are_rejected()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);

        var response = await PostEntryAsync(app, c, c.Owner, 0.00001m, 0.00001m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(app, "SELECT count(*) FROM accounting.journal_entries"));
    }

    [Fact]
    public async Task Database_refuses_an_unbalanced_entry_even_for_the_owner_role()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);

        await using var connection = new NpgsqlConnection(app.Db.OwnerConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        Guid entryId;
        await using (var insert = new NpgsqlCommand($"""
            INSERT INTO accounting.journal_entries (company_id, entry_number, entry_date, memo, posted_by)
            SELECT '{c.CompanyId}', 1, DATE '2026-11-16', 'bad', id FROM identity.users
             WHERE lower(email_normalized) = lower('{OwnerEmail}')
            RETURNING id
            """, connection, tx))
        {
            entryId = (Guid)(await insert.ExecuteScalarAsync())!;
        }

        await using (var lines = new NpgsqlCommand($"""
            INSERT INTO accounting.journal_lines (entry_id, company_id, line_no, account_id, debit, credit)
            VALUES ('{entryId}', '{c.CompanyId}', 1, '{c.Cash}', 100, 0),
                   ('{entryId}', '{c.CompanyId}', 2, '{c.Revenue}', 0, 50)
            """, connection, tx))
        {
            await lines.ExecuteNonQueryAsync();
        }

        var ex = await Assert.ThrowsAsync<PostgresException>(() => tx.CommitAsync());
        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public async Task Posted_lines_cannot_be_updated_or_deleted()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);
        await CreatedIdAsync(await PostEntryAsync(app, c, c.Owner, 50m, 50m));

        var update = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecAsync(app, "UPDATE accounting.journal_lines SET debit = 1 WHERE debit > 0"));
        Assert.Equal("55000", update.SqlState);

        var delete = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecAsync(app, "DELETE FROM accounting.journal_entries"));
        Assert.Equal("55000", delete.SqlState);
    }

    [Fact]
    public async Task App_role_cannot_write_ledger_tables_directly()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);

        await using var connection = new NpgsqlConnection(app.Db.AppConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"INSERT INTO accounting.accounts (company_id, code, name, account_type) " +
            $"VALUES ('{c.CompanyId}', '9999', 'Sneaky', 'asset')", connection);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task Retrying_with_the_same_idempotency_key_does_not_post_twice()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);

        var first = await CreatedIdAsync(await PostEntryAsync(app, c, c.Owner, 75m, 75m, key: "sale-0001"));
        var second = await CreatedIdAsync(await PostEntryAsync(app, c, c.Owner, 75m, 75m, key: "sale-0001"));

        Assert.Equal(first, second);
        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM accounting.journal_entries"));
    }

    [Fact]
    public async Task Reversal_nets_to_zero_and_a_second_reversal_is_a_conflict()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);
        var entryId = await CreatedIdAsync(await PostEntryAsync(app, c, c.Owner, 60m, 60m));
        var url = $"/api/v1/companies/{c.CompanyId}/accounting/journal-entries/{entryId}/reverse";

        await CreatedIdAsync(await SendAsync(app, HttpMethod.Post, url, c.Owner, new { entryDate = EntryDate }, "rev-1"));
        var again = await SendAsync(app, HttpMethod.Post, url, c.Owner, new { entryDate = EntryDate }, "rev-2");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var tb = await TrialBalanceAsync(app, c);
        Assert.True(tb.GetProperty("isBalanced").GetBoolean());
        foreach (var row in tb.GetProperty("rows").EnumerateArray())
            Assert.Equal(row.GetProperty("totalDebit").GetDecimal(), row.GetProperty("totalCredit").GetDecimal());
        Assert.Equal(2, await CountAsync(app, "SELECT count(*) FROM accounting.journal_entries"));
    }

    [Fact]
    public async Task A_plain_member_can_read_the_ledger_but_not_post()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);

        var read = await SendAsync(app, HttpMethod.Get,
            $"/api/v1/companies/{c.CompanyId}/accounting/accounts", c.Member);
        var post = await PostEntryAsync(app, c, c.Member, 10m, 10m);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        Assert.Equal(0, await CountAsync(app, "SELECT count(*) FROM accounting.journal_entries"));
    }

    [Fact]
    public async Task A_user_outside_the_company_cannot_see_or_post_to_its_ledger()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var c = await SetUpAsync(app);
        await CreatedIdAsync(await PostEntryAsync(app, c, c.Owner, 40m, 40m));

        var accounts = await SendAsync(app, HttpMethod.Get,
            $"/api/v1/companies/{c.CompanyId}/accounting/accounts", c.Outsider);
        var entries = await SendAsync(app, HttpMethod.Get,
            $"/api/v1/companies/{c.CompanyId}/accounting/journal-entries", c.Outsider);
        var trial = await SendAsync(app, HttpMethod.Get,
            $"/api/v1/companies/{c.CompanyId}/accounting/trial-balance", c.Outsider);
        var post = await PostEntryAsync(app, c, c.Outsider, 10m, 10m);

        Assert.Equal(HttpStatusCode.NotFound, accounts.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, entries.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, trial.StatusCode);
        Assert.NotEqual(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal(1, await CountAsync(app, "SELECT count(*) FROM accounting.journal_entries"));
    }

    [Fact]
    public async Task Ledger_endpoints_require_a_session()
    {
        await using var app = await IdentityTestApp.StartAsync(postgres);
        var response = await app.Client.GetAsync(
            $"/api/v1/companies/{Guid.NewGuid()}/accounting/accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
