using FlexiCore.ActivationEngine.Services;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class RenewalLedgerReadTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fc-renewal-ledger-" + Guid.NewGuid().ToString("N"));
    private readonly string _ledger;

    public RenewalLedgerReadTests()
    {
        Directory.CreateDirectory(_directory);
        _ledger = Path.Combine(_directory, "issued.jsonl");
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }

    [Fact]
    public async Task Renewal_predecessor_and_actor_are_read_from_backward_compatible_ledger_row()
    {
        var customerId = Guid.NewGuid();
        var bytes = System.Text.Json.JsonSerializer.Serialize(new
        {
            recordedAt = "2026-06-01T00:00:00Z",
            licenseId = "LIC-NEW-1",
            licensee = "Customer",
            keyId = "test-key",
            notBefore = "2026-06-01T00:00:00Z",
            expiresAt = "2027-06-01T00:00:00Z",
            graceDays = 0,
            maxInstallations = 1,
            installationId = new string('a', 64),
            customerId,
            issuedBy = "owner",
            renewedFromLicenseId = "LIC-OLD-1",
            fileSha256 = new string('b', 64),
        });
        await File.WriteAllTextAsync(_ledger, bytes + Environment.NewLine);

        var snapshot = LedgerReader.Read(_ledger);

        Assert.Equal(0, snapshot.UnreadableLines);
        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal("LIC-OLD-1", entry.RenewedFromLicenseId);
        Assert.Equal(customerId, entry.CustomerId);
        Assert.Equal("owner", entry.IssuedBy);
    }
}
