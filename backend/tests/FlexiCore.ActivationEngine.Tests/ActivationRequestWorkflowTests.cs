using FlexiCore.ActivationEngine.Services;
using FlexiCore.Licensing;
using FlexiCore.LicenseTool;

namespace FlexiCore.ActivationEngine.Tests;

public sealed class ActivationRequestWorkflowTests
{
    private static (CustomerRegistry Customers, CustomerRecord Customer, ActivationRequestRegistry Requests, ActivationRequestRecord Request)
        CreatePending(Owner owner, string customerName = "Acme Limited")
    {
        var customers = new CustomerRegistry(owner.Paths, owner.Time);
        var customer = customers.Register(new CustomerInput(customerName, Email: "ops@example.test"));
        var requests = new ActivationRequestRegistry(owner.Paths, customers, owner.Time);
        var installation = InstallationIds.FromMachineSource(Guid.NewGuid().ToString("N"));
        var code = InstallationIds.ToRequestCode(installation);
        var request = requests.Register(code, customer.Id, requests.CurrentActor);
        return (customers, customer, requests, request);
    }

    [Fact]
    public void Register_is_idempotent_by_installation_and_persists_a_pending_review_record()
    {
        using var owner = new Owner();
        var (customers, customer, requests, request) = CreatePending(owner);
        var again = requests.Register(InstallationIds.ToRequestCode(request.InstallationId), customer.Id, requests.CurrentActor);

        Assert.Equal(request.Id, again.Id);
        Assert.Equal(ActivationRequestStatus.Pending, Assert.Single(requests.List()).Status);
        Assert.Equal("Acme Limited", again.CustomerName);
        Assert.Equal(LicenseLimits.ExpectedProduct, again.Product);
        Assert.Equal(ActivationRequestStatus.Pending,
            Assert.Single(new ActivationRequestRegistry(owner.Paths, customers, owner.Time).List()).Status);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(owner.Paths.ActivationRequestsPath));
    }

    [Fact]
    public void Unknown_or_inactive_customers_and_malformed_request_codes_are_rejected()
    {
        using var owner = new Owner();
        var customers = new CustomerRegistry(owner.Paths, owner.Time);
        var requests = new ActivationRequestRegistry(owner.Paths, customers, owner.Time);
        var reviewer = requests.CurrentActor;
        var code = InstallationIds.ToRequestCode(InstallationIds.FromMachineSource("unit-test-device"));
        Assert.Throws<LicenseToolException>(() => requests.Register(code, Guid.NewGuid(), reviewer));

        var inactive = customers.Register(new CustomerInput("Inactive Co"));
        customers.SetStatus(inactive.Id, CustomerStatus.Inactive);
        Assert.Throws<LicenseToolException>(() => requests.Register(code, inactive.Id, reviewer));
        Assert.Throws<LicenseToolException>(() => requests.Register("FCR1-AAAA", inactive.Id, reviewer));
        Assert.False(File.Exists(owner.Paths.ActivationRequestsPath));
    }

    [Fact]
    public void Review_transitions_require_local_reviewer_and_rejection_reason_and_are_immutable_after_decision()
    {
        using var owner = new Owner();
        var (_, _, requests, request) = CreatePending(owner);

        Assert.Throws<LicenseToolException>(() => requests.Transition(request.Id, ActivationRequestStatus.UnderReview, "some-other-account"));
        var reviewing = requests.Transition(request.Id, ActivationRequestStatus.UnderReview, requests.CurrentActor);
        Assert.Equal(ActivationRequestStatus.UnderReview, reviewing.Status);
        Assert.Throws<LicenseToolException>(() => requests.Transition(request.Id, ActivationRequestStatus.UnderReview, requests.CurrentActor));
        Assert.Throws<LicenseToolException>(() => requests.Transition(request.Id, ActivationRequestStatus.Rejected, requests.CurrentActor));
        var rejected = requests.Transition(request.Id, ActivationRequestStatus.Rejected, requests.CurrentActor, "Customer could not be verified.");
        Assert.Equal(ActivationRequestStatus.Rejected, rejected.Status);
        Assert.Equal(3, rejected.History.Count);
        Assert.Equal("Customer could not be verified.", rejected.History[^1].Reason);
        Assert.Throws<LicenseToolException>(() => requests.Transition(request.Id, ActivationRequestStatus.Approved, requests.CurrentActor));
        Assert.Equal(ActivationRequestStatus.Rejected, requests.Find(request.Id)!.Status);
    }

    [Fact]
    public void Pending_request_can_be_cancelled_only_with_a_reason_and_filtering_uses_customer_and_status()
    {
        using var owner = new Owner();
        var (_, customer, requests, request) = CreatePending(owner);
        Assert.Empty(requests.List(ActivationRequestStatus.Approved));
        Assert.Single(requests.List(customerId: customer.Id, search: request.Id.ToString("D")));
        Assert.Throws<LicenseToolException>(() => requests.Transition(request.Id, ActivationRequestStatus.Cancelled, requests.CurrentActor));
        Assert.Equal(ActivationRequestStatus.Cancelled,
            requests.Transition(request.Id, ActivationRequestStatus.Cancelled, requests.CurrentActor, "Duplicate request withdrawn.").Status);
        Assert.Throws<LicenseToolException>(() => requests.Transition(request.Id, ActivationRequestStatus.UnderReview, requests.CurrentActor));
    }

    [Fact]
    public void Corrupt_request_registry_is_preserved_and_fails_closed()
    {
        using var owner = new Owner();
        var customers = new CustomerRegistry(owner.Paths, owner.Time);
        Directory.CreateDirectory(owner.Paths.Root);
        File.WriteAllText(owner.Paths.ActivationRequestsPath, "{corrupt");
        var original = File.ReadAllBytes(owner.Paths.ActivationRequestsPath);
        var requests = new ActivationRequestRegistry(owner.Paths, customers, owner.Time);

        Assert.Throws<LicenseToolException>(() => requests.List());
        Assert.Equal(original, File.ReadAllBytes(owner.Paths.ActivationRequestsPath));
    }

    [Fact]
    public void Only_an_approved_active_customer_request_can_issue_and_success_is_linked_durably()
    {
        using var owner = new Owner();
        var key = owner.CreateKey();
        var (customers, customer, requests, pending) = CreatePending(owner);
        var strictIssuer = new IssueService(owner.Paths, owner.Time, owner.Keys, owner.KeyManager,
            requests, customers, requireApprovedRequest: true);
        var output = owner.Out("approved-request.lic");
        IssueInput Input(ActivationRequestRecord r, Guid customerId, string licensee, string path) =>
            owner.Input(key, "FC-REQUEST-0001", licensee, r.InstallationId, path) with
            {
                ActivationRequestId = r.Id,
                CustomerId = customerId,
                IssuedBy = requests.CurrentActor,
            };

        Assert.Throws<LicenseToolException>(() => strictIssuer.Issue(Input(pending, customer.Id, customer.Name, output)));
        Assert.False(File.Exists(output));
        Assert.Empty(LedgerReader.Read(owner.Paths.LedgerPath).Entries);

        var reviewing = requests.Transition(pending.Id, ActivationRequestStatus.UnderReview, requests.CurrentActor, "Documents reviewed.");
        var approved = requests.Transition(reviewing.Id, ActivationRequestStatus.Approved, requests.CurrentActor, "Customer and installation verified.");
        Assert.Throws<LicenseToolException>(() => strictIssuer.Issue(Input(approved, Guid.NewGuid(), customer.Name, output)));
        Assert.False(File.Exists(output));

        var issued = strictIssuer.Issue(Input(approved, customer.Id, customer.Name, output));
        Assert.Equal(LicenseStatus.Valid, owner.Verify(key, issued.LicensePath).Status);
        var ledger = Assert.Single(LedgerReader.Read(owner.Paths.LedgerPath).Entries);
        Assert.Equal(approved.Id, ledger.ActivationRequestId);
        Assert.Equal(customer.Id, ledger.CustomerId);
        Assert.Equal(approved.InstallationId, ledger.InstallationId);
        Assert.Equal(requests.CurrentActor, ledger.IssuedBy);
        Assert.Equal(ActivationRequestStatus.Issued, requests.Find(approved.Id)!.Status);
        Assert.Throws<LicenseToolException>(() => strictIssuer.Issue(Input(approved, customer.Id, customer.Name, owner.Out("second.lic"))));
        Assert.False(File.Exists(owner.Out("second.lic")));
    }

    [Fact]
    public void Strict_issuance_refuses_missing_or_wrong_customer_binding_without_mutating_approval()
    {
        using var owner = new Owner();
        var key = owner.CreateKey();
        var (customers, customer, requests, pending) = CreatePending(owner);
        var reviewing = requests.Transition(pending.Id, ActivationRequestStatus.UnderReview, requests.CurrentActor, "Reviewed.");
        var approved = requests.Transition(reviewing.Id, ActivationRequestStatus.Approved, requests.CurrentActor, "Approved.");
        var strictIssuer = new IssueService(owner.Paths, owner.Time, owner.Keys, owner.KeyManager, requests, customers, true);
        var invalid = owner.Input(key, "FC-REQUEST-0002", "Wrong Customer", approved.InstallationId, owner.Out("wrong-customer.lic")) with
        {
            ActivationRequestId = approved.Id,
            CustomerId = customer.Id,
            IssuedBy = requests.CurrentActor,
        };

        Assert.Throws<LicenseToolException>(() => strictIssuer.Issue(invalid));
        Assert.False(File.Exists(invalid.OutputPath));
        Assert.Equal(ActivationRequestStatus.Approved, requests.Find(approved.Id)!.Status);
        Assert.Empty(LedgerReader.Read(owner.Paths.LedgerPath).Entries);
    }
}
