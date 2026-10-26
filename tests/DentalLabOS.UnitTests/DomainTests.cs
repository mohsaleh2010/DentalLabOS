using DentalLabOS.Domain.Common;
using DentalLabOS.Domain.Organization;

namespace DentalLabOS.UnitTests;

public sealed class DocumentSequenceTests
{
    [Fact]
    public void Yearly_sequence_includes_two_digit_year()
    {
        var seq = new DocumentSequence { Prefix = "WO", Year = 2026, Padding = 6, ResetYearly = true };
        Assert.Equal("WO-26-000412", seq.Format(412));
    }

    [Fact]
    public void Continuous_sequence_has_no_year()
    {
        var seq = new DocumentSequence { Prefix = "INV", Year = 2026, Padding = 5, ResetYearly = false };
        Assert.Equal("INV-00007", seq.Format(7));
    }

    [Fact]
    public void Numbers_longer_than_padding_are_not_truncated()
    {
        var seq = new DocumentSequence { Prefix = "JE", Year = 2026, Padding = 3, ResetYearly = false };
        Assert.Equal("JE-12345", seq.Format(12345));
    }
}

public sealed class ApprovalRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Approver_different_from_requester_can_approve()
    {
        var req = new ApprovalRequest { RequestedByUserId = Guid.NewGuid() };
        var approver = Guid.NewGuid();
        req.Decide(approver, approve: true, reason: null, allowSelfApproval: false, Now);
        Assert.Equal(ApprovalStatus.Approved, req.Status);
        Assert.Equal(approver, req.DecidedByUserId);
        Assert.Equal(Now, req.DecidedAtUtc);
    }

    [Fact]
    public void Requester_cannot_approve_own_request_by_default()
    {
        var user = Guid.NewGuid();
        var req = new ApprovalRequest { RequestedByUserId = user };
        var ex = Assert.Throws<DomainException>(() => req.Decide(user, true, null, allowSelfApproval: false, Now));
        Assert.Equal("Errors.Approval.SelfApproval", ex.MessageKey);
        Assert.Equal(ApprovalStatus.Pending, req.Status);
    }

    [Fact]
    public void Self_approval_allowed_when_tenant_permits()
    {
        var user = Guid.NewGuid();
        var req = new ApprovalRequest { RequestedByUserId = user };
        req.Decide(user, true, null, allowSelfApproval: true, Now);
        Assert.Equal(ApprovalStatus.Approved, req.Status);
    }

    [Fact]
    public void Rejection_requires_reason()
    {
        var req = new ApprovalRequest { RequestedByUserId = Guid.NewGuid() };
        var ex = Assert.Throws<DomainException>(() => req.Decide(Guid.NewGuid(), false, " ", false, Now));
        Assert.Equal("Errors.ReasonRequired", ex.MessageKey);
    }

    [Fact]
    public void A_decided_request_cannot_be_decided_again()
    {
        var req = new ApprovalRequest { RequestedByUserId = Guid.NewGuid() };
        req.Decide(Guid.NewGuid(), false, "price too low", false, Now);
        var ex = Assert.Throws<DomainException>(() => req.Decide(Guid.NewGuid(), true, null, false, Now));
        Assert.Equal("Errors.Approval.AlreadyDecided", ex.MessageKey);
    }

    [Fact]
    public void New_entities_get_time_ordered_ids()
    {
        var a = new ApprovalRequest();
        var b = new ApprovalRequest();
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(7, a.Id.Version);
    }
}
