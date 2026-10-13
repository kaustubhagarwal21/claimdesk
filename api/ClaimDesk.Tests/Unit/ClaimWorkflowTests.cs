using ClaimDesk.Api.Domain;

namespace ClaimDesk.Tests.Unit;

public class ClaimWorkflowTests
{
    private static readonly DateOnly TermStart = new(2026, 1, 1);
    private static readonly DateOnly TermEnd = new(2026, 12, 31);

    // ---- transitions ----

    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Approved)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Paid)]
    public void Allowed_transitions(ClaimStatus from, ClaimStatus to)
    {
        Assert.True(ClaimWorkflow.CanTransition(from, to));
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Approved)]      // review cannot be skipped
    [InlineData(ClaimStatus.Submitted, ClaimStatus.Paid)]
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.Paid)]        // approval cannot be skipped
    [InlineData(ClaimStatus.UnderReview, ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.Approved, ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.Rejected, ClaimStatus.Approved)]       // Rejected is final
    [InlineData(ClaimStatus.Rejected, ClaimStatus.Paid)]
    [InlineData(ClaimStatus.Paid, ClaimStatus.Approved)]           // Paid is final
    public void Other_transitions_are_not_allowed(ClaimStatus from, ClaimStatus to)
    {
        Assert.False(ClaimWorkflow.CanTransition(from, to));
    }

    [Fact]
    public void Moving_to_a_status_that_is_not_next_breaks_a_business_rule()
    {
        Assert.Throws<BusinessRuleException>(() =>
            ClaimWorkflow.EnsureCanMove(Role.Manager, ClaimStatus.Submitted, ClaimStatus.Paid));
    }

    // ---- role checks ----

    [Theory]
    [InlineData(Role.Agent, ClaimStatus.Submitted)]
    [InlineData(Role.Adjuster, ClaimStatus.UnderReview)]
    [InlineData(Role.Adjuster, ClaimStatus.Approved)]
    [InlineData(Role.Manager, ClaimStatus.Approved)]
    [InlineData(Role.Adjuster, ClaimStatus.Rejected)]
    [InlineData(Role.Manager, ClaimStatus.Rejected)]
    [InlineData(Role.Manager, ClaimStatus.Paid)]
    public void Roles_that_may_set_a_status(Role role, ClaimStatus status)
    {
        ClaimWorkflow.EnsureRoleCanSet(role, status);   // does not throw
    }

    [Theory]
    [InlineData(Role.Adjuster, ClaimStatus.Submitted)]
    [InlineData(Role.Manager, ClaimStatus.Submitted)]
    [InlineData(Role.Agent, ClaimStatus.UnderReview)]
    [InlineData(Role.Manager, ClaimStatus.UnderReview)]
    [InlineData(Role.Agent, ClaimStatus.Approved)]
    [InlineData(Role.Agent, ClaimStatus.Rejected)]
    [InlineData(Role.Agent, ClaimStatus.Paid)]
    [InlineData(Role.Adjuster, ClaimStatus.Paid)]
    public void Roles_that_may_not_set_a_status(Role role, ClaimStatus status)
    {
        Assert.Throws<ForbiddenException>(() => ClaimWorkflow.EnsureRoleCanSet(role, status));
    }

    [Fact]
    public void The_role_is_checked_before_the_transition()
    {
        // Both are wrong here: an Agent may not pay, and Submitted cannot go to Paid.
        Assert.Throws<ForbiddenException>(() =>
            ClaimWorkflow.EnsureCanMove(Role.Agent, ClaimStatus.Submitted, ClaimStatus.Paid));
    }

    // ---- approval: maker-checker and the Manager threshold ----

    [Fact]
    public void The_reviewer_cannot_approve_the_same_claim()
    {
        var error = Assert.Throws<BusinessRuleException>(() =>
            ClaimWorkflow.EnsureCanApprove(approverId: 2, Role.Adjuster, reviewedByUserId: 2, amount: 5_000m, remainingCover: 50_000m));

        Assert.Contains("reviewed", error.Message);
    }

    [Fact]
    public void A_different_adjuster_can_approve()
    {
        ClaimWorkflow.EnsureCanApprove(approverId: 3, Role.Adjuster, reviewedByUserId: 2, amount: 5_000m, remainingCover: 50_000m);
    }

    [Fact]
    public void An_adjuster_can_approve_exactly_the_threshold()
    {
        ClaimWorkflow.EnsureCanApprove(3, Role.Adjuster, 2, ClaimWorkflow.ManagerApprovalThreshold, 500_000m);
    }

    [Fact]
    public void An_adjuster_cannot_approve_above_the_threshold()
    {
        Assert.Throws<ForbiddenException>(() =>
            ClaimWorkflow.EnsureCanApprove(3, Role.Adjuster, 2, ClaimWorkflow.ManagerApprovalThreshold + 0.01m, 500_000m));
    }

    [Fact]
    public void A_manager_can_approve_above_the_threshold()
    {
        ClaimWorkflow.EnsureCanApprove(4, Role.Manager, 2, 250_000m, 500_000m);
    }

    [Fact]
    public void Approval_is_refused_when_the_cover_has_been_used_up()
    {
        Assert.Throws<BusinessRuleException>(() =>
            ClaimWorkflow.EnsureCanApprove(3, Role.Adjuster, 2, amount: 30_000m, remainingCover: 20_000m));
    }

    // ---- filing ----

    [Fact]
    public void A_claim_inside_the_term_and_within_cover_can_be_filed()
    {
        ClaimWorkflow.EnsureCanFile(PolicyStatus.Active, TermStart, TermEnd, new DateOnly(2026, 6, 15), 10_000m, 10_000m);
    }

    [Theory]
    [InlineData(PolicyStatus.Cancelled)]
    [InlineData(PolicyStatus.Expired)]
    public void A_claim_needs_an_active_policy(PolicyStatus status)
    {
        Assert.Throws<BusinessRuleException>(() =>
            ClaimWorkflow.EnsureCanFile(status, TermStart, TermEnd, new DateOnly(2026, 6, 15), 10_000m, 50_000m));
    }

    [Theory]
    [InlineData(2025, 12, 31)]   // the day before the term starts
    [InlineData(2027, 1, 1)]     // the day after the term ends
    public void The_incident_date_must_be_inside_the_term(int year, int month, int day)
    {
        Assert.Throws<BusinessRuleException>(() =>
            ClaimWorkflow.EnsureCanFile(PolicyStatus.Active, TermStart, TermEnd, new DateOnly(year, month, day), 10_000m, 50_000m));
    }

    [Theory]
    [InlineData(2026, 1, 1)]     // first day of the term
    [InlineData(2026, 12, 31)]   // last day of the term
    public void The_first_and_last_day_of_the_term_are_covered(int year, int month, int day)
    {
        ClaimWorkflow.EnsureCanFile(PolicyStatus.Active, TermStart, TermEnd, new DateOnly(year, month, day), 10_000m, 50_000m);
    }

    [Fact]
    public void The_amount_cannot_be_above_the_remaining_cover()
    {
        Assert.Throws<BusinessRuleException>(() =>
            ClaimWorkflow.EnsureCanFile(PolicyStatus.Active, TermStart, TermEnd, new DateOnly(2026, 6, 15), 50_000.01m, 50_000m));
    }
}
