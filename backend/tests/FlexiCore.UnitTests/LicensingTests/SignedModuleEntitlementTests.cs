using System.Text;
using FlexiCore.Licensing;
using FlexiCore.Migrations;

namespace FlexiCore.UnitTests.LicensingTests;

public sealed class SignedModuleEntitlementTests : IDisposable
{
    private readonly TestSigner signer = new();
    private readonly FixedTimeProvider time = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly Guid organizationId = Guid.Parse("2d9c2d0e-4c71-4c06-a75b-f9d943521aad");

    [Fact]
    public void VerifierAcceptsBoundSignedModuleFeaturesAndLimits()
    {
        var result = Verifier().Verify(Artifact());

        Assert.True(result.IsValid);
        Assert.Equal(organizationId, result.Payload!.OrganizationId);
        Assert.Equal("acme-corp", result.Payload.CustomerId);
        Assert.Equal(7, result.Payload.Modules.Single().MaxCompanies);
        Assert.Equal("companies.create", result.Payload.Modules.Single().FeatureKey);
    }

    [Fact]
    public void VerifierRejectsSignatureTamperingAndUnknownClaims()
    {
        var artifact = Artifact();
        var text = Encoding.UTF8.GetString(artifact);
        var payloadStart = text.IndexOf("\"payload\":\"", StringComparison.Ordinal) + "\"payload\":\"".Length;
        var tampered = Encoding.UTF8.GetBytes(text[..payloadStart] + (text[payloadStart] == 'A' ? 'B' : 'A') + text[(payloadStart + 1)..]);
        Assert.Equal(ModuleEntitlementError.InvalidSignature, Verifier().Verify(tampered).Error);

        var extra = Encoding.UTF8.GetBytes(Payload().Replace("\"revision\":1", "\"revision\":1,\"unexpected\":true", StringComparison.Ordinal));
        Assert.Equal(ModuleEntitlementError.InvalidPayload,
            Verifier().Verify(TestSigner.Envelope(extra, signer.Sign(extra))).Error);
    }

    [Fact]
    public void VerifierRejectsExpiredAndMalformedFeatureRevision()
    {
        var expired = Payload().Replace("2026-07-01T00:00:00Z", "2026-05-31T00:00:00Z", StringComparison.Ordinal);
        Assert.Equal(ModuleEntitlementError.InvalidPayload,
            Verifier().Verify(Sign(expired)).Error); // validUntil cannot precede validFrom

        var badModule = Payload().Replace("companies.create", "../admin", StringComparison.Ordinal);
        Assert.Equal(ModuleEntitlementError.InvalidPayload, Verifier().Verify(Sign(badModule)).Error);
    }

    [Fact]
    public void MigrationPersistsSignedArtifactsAndRevisionRows()
    {
        var migration = MigrationRunner.LoadEmbeddedScripts().Single(x => x.Version == "0010_licensing_signed_entitlements");
        Assert.Contains("licensing.entitlement_revisions", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("signed_entitlement bytea NOT NULL", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("fk_entitlements_signed_revision", migration.Sql, StringComparison.Ordinal);
        Assert.Contains("REVOKE UPDATE, DELETE, TRUNCATE", migration.Sql, StringComparison.Ordinal);
    }

    private SignedModuleEntitlementVerifier Verifier() => new([signer.Trusted()], time);
    private byte[] Artifact() => Sign(Payload());
    private byte[] Sign(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        return TestSigner.Envelope(bytes, signer.Sign(bytes));
    }

    private string Payload() => """
        {"schemaVersion":1,"licenseId":"LIC-0001","organizationId":"2d9c2d0e-4c71-4c06-a75b-f9d943521aad",
         "customerId":"acme-corp","revision":1,"issuedAt":"2026-06-01T00:00:00Z",
         "validFrom":"2026-06-01T00:00:00Z","validUntil":"2026-07-01T00:00:00Z",
         "modules":[{"moduleKey":"core","featureKey":"companies.create","enabled":true,
           "maxUsers":100,"maxDevices":null,"maxCompanies":7,"maxBranches":null}]}
        """;

    public void Dispose() => signer.Dispose();
}
