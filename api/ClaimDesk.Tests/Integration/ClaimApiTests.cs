using System.Net;
using ClaimDesk.Api.Data;
using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;
using ClaimDesk.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimDesk.Tests.Integration;

// The claim workflow end to end, including the audit trigger and optimistic concurrency.
public class ClaimApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Filing_a_claim_returns_201_and_the_trigger_writes_the_first_audit_row()
    {
        var policy = await IssuePolicyAsync();

        var response = await FileClaimAsync(Asha, policy.Id, 20_000m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var claim = await ReadAsync<ClaimDto>(response);
        Assert.Matches(@"^CLM-\d{6}$", claim.ClaimNumber);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal(20_000m, claim.Amount);
        Assert.Equal(policy.PolicyNumber, claim.PolicyNumber);
        Assert.Equal(PolicyType.Motor, claim.PolicyType);
        Assert.Equal("Asha", claim.FiledBy);
        Assert.Null(claim.ReviewedBy);
        Assert.Null(claim.ApprovedBy);
        Assert.Equal(8, claim.RowVersion.Length);
        Assert.Equal(DateTimeKind.Utc, claim.CreatedAt.Kind);
        Assert.EndsWith($"/api/claims/{claim.Id}", response.Headers.Location!.AbsolutePath);

        // The application never inserts audit rows; this one comes from trg_Claims_StatusAudit.
        var entry = Assert.Single(await GetAuditAsync(claim.Id));
        Assert.Null(entry.OldStatus);
        Assert.Equal(ClaimStatus.Submitted, entry.NewStatus);
        Assert.Equal("Asha", entry.ChangedBy);
        Assert.InRange(entry.ChangedAt, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task The_happy_path_ends_in_Paid_with_the_audit_trail_in_order()
    {
        var policy = await IssuePolicyAsync(sumInsured: 500_000m);
        var claim = await FileClaimAsync(policy.Id, 45_000m);

        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));
        Assert.Equal(ClaimStatus.UnderReview, claim.Status);
        Assert.Equal("Ravi", claim.ReviewedBy);

        claim = await ReadAsync<ClaimDto>(ApproveAsync(Meera, claim));
        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal("Meera", claim.ApprovedBy);

        claim = await ReadAsync<ClaimDto>(PayAsync(Kiran, claim));
        Assert.Equal(ClaimStatus.Paid, claim.Status);

        var audit = await GetAuditAsync(claim.Id);
        (ClaimStatus? Old, ClaimStatus New, string By)[] expected =
        [
            (null, ClaimStatus.Submitted, "Asha"),
            (ClaimStatus.Submitted, ClaimStatus.UnderReview, "Ravi"),
            (ClaimStatus.UnderReview, ClaimStatus.Approved, "Meera"),
            (ClaimStatus.Approved, ClaimStatus.Paid, "Kiran"),
        ];
        Assert.Equal(expected, audit.Select(a => (a.OldStatus, a.NewStatus, a.ChangedBy)));
        Assert.Equal(audit.Select(a => a.ChangedAt).Order(), audit.Select(a => a.ChangedAt));

        // An approved or paid claim uses up cover on the policy.
        Assert.Equal(455_000m, (await GetPolicyAsync(policy.Id)).RemainingCover);
    }

    [Fact]
    public async Task The_reviewer_cannot_approve_the_claim_but_another_adjuster_can()
    {
        var policy = await IssuePolicyAsync();
        var claim = await FileClaimAsync(policy.Id, 30_000m);
        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));

        var problem = await ReadProblemAsync(ApproveAsync(Ravi, claim), HttpStatusCode.Conflict);

        Assert.Contains("reviewed", problem.GetProperty("detail").GetString());
        Assert.Equal(ClaimStatus.UnderReview, (await GetClaimAsync(claim.Id)).Status);

        claim = await ReadAsync<ClaimDto>(ApproveAsync(Meera, claim));
        Assert.Equal(ClaimStatus.Approved, claim.Status);
    }

    [Fact]
    public async Task A_claim_above_100000_needs_a_manager()
    {
        var policy = await IssuePolicyAsync(sumInsured: 500_000m);
        var claim = await FileClaimAsync(policy.Id, 100_000.01m);
        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));

        await ReadProblemAsync(ApproveAsync(Meera, claim), HttpStatusCode.Forbidden);

        claim = await ReadAsync<ClaimDto>(ApproveAsync(Kiran, claim));
        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal("Kiran", claim.ApprovedBy);
    }

    [Fact]
    public async Task An_adjuster_can_approve_exactly_100000()
    {
        var policy = await IssuePolicyAsync(sumInsured: 500_000m);
        var claim = await FileClaimAsync(policy.Id, 100_000m);
        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));

        claim = await ReadAsync<ClaimDto>(ApproveAsync(Meera, claim));

        Assert.Equal(ClaimStatus.Approved, claim.Status);
    }

    [Fact]
    public async Task A_claim_above_the_remaining_cover_is_refused()
    {
        var policy = await IssuePolicyAsync(sumInsured: 50_000m);

        // Above the full cover.
        await ReadProblemAsync(FileClaimAsync(Asha, policy.Id, 50_000.01m), HttpStatusCode.Conflict);

        // An approved claim of 40000 leaves 10000.
        var claim = await FileClaimAsync(policy.Id, 40_000m);
        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));
        await ReadAsync<ClaimDto>(ApproveAsync(Meera, claim));
        Assert.Equal(10_000m, (await GetPolicyAsync(policy.Id)).RemainingCover);

        var problem = await ReadProblemAsync(FileClaimAsync(Asha, policy.Id, 10_000.01m), HttpStatusCode.Conflict);
        Assert.Contains("remaining cover", problem.GetProperty("detail").GetString());

        await FileClaimAsync(policy.Id, 10_000m);   // exactly the remaining cover is accepted
    }

    [Fact]
    public async Task Approval_is_refused_when_other_claims_have_used_up_the_cover()
    {
        // Both claims fit when they are filed, but not together.
        var policy = await IssuePolicyAsync(sumInsured: 50_000m);
        var first = await FileClaimAsync(policy.Id, 30_000m);
        var second = await FileClaimAsync(policy.Id, 30_000m);
        first = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, first));
        second = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, second));

        await ReadAsync<ClaimDto>(ApproveAsync(Meera, first));

        await ReadProblemAsync(ApproveAsync(Meera, second), HttpStatusCode.Conflict);
        Assert.Equal(20_000m, (await GetPolicyAsync(policy.Id)).RemainingCover);
    }

    [Fact]
    public async Task A_stale_row_version_returns_409()
    {
        var policy = await IssuePolicyAsync();
        var submitted = await FileClaimAsync(policy.Id, 20_000m);
        var underReview = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, submitted));
        Assert.NotEqual(submitted.RowVersion, underReview.RowVersion);

        // Meera still holds the copy she read before Ravi's review changed the row.
        var problem = await ReadProblemAsync(ApproveAsync(Meera, submitted), HttpStatusCode.Conflict);

        Assert.Equal(GlobalExceptionHandler.ConcurrencyTitle, problem.GetProperty("title").GetString());
        Assert.Equal(ClaimStatus.UnderReview, (await GetClaimAsync(submitted.Id)).Status);
        Assert.Equal(2, (await GetAuditAsync(submitted.Id)).Count);

        // Reject and pay use the same check.
        await ReadProblemAsync(RejectAsync(Meera, submitted, "Not covered by this policy"), HttpStatusCode.Conflict);

        // With the current row version the approval goes through.
        var approved = await ReadAsync<ClaimDto>(ApproveAsync(Meera, underReview));
        await ReadProblemAsync(PayAsync(Kiran, underReview), HttpStatusCode.Conflict);
        Assert.Equal(ClaimStatus.Paid, (await ReadAsync<ClaimDto>(PayAsync(Kiran, approved))).Status);
    }

    [Fact]
    public async Task Role_checks_return_403()
    {
        var policy = await IssuePolicyAsync();

        // Only an Agent files a claim.
        await ReadProblemAsync(FileClaimAsync(Ravi, policy.Id, 5_000m), HttpStatusCode.Forbidden);
        await ReadProblemAsync(FileClaimAsync(Kiran, policy.Id, 5_000m), HttpStatusCode.Forbidden);

        // Only an Adjuster reviews.
        var claim = await FileClaimAsync(policy.Id, 5_000m);
        await ReadProblemAsync(ReviewAsync(Asha, claim), HttpStatusCode.Forbidden);
        await ReadProblemAsync(ReviewAsync(Kiran, claim), HttpStatusCode.Forbidden);

        // An Agent neither approves nor rejects.
        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));
        await ReadProblemAsync(ApproveAsync(Asha, claim), HttpStatusCode.Forbidden);
        await ReadProblemAsync(RejectAsync(Asha, claim, "Agents cannot reject"), HttpStatusCode.Forbidden);

        // Only a Manager pays.
        claim = await ReadAsync<ClaimDto>(ApproveAsync(Meera, claim));
        await ReadProblemAsync(PayAsync(Meera, claim), HttpStatusCode.Forbidden);
        await ReadProblemAsync(PayAsync(Asha, claim), HttpStatusCode.Forbidden);

        Assert.Equal(ClaimStatus.Approved, (await GetClaimAsync(claim.Id)).Status);
    }

    [Fact]
    public async Task Steps_cannot_be_skipped_or_repeated()
    {
        var policy = await IssuePolicyAsync();
        var claim = await FileClaimAsync(policy.Id, 5_000m);

        // Submitted: not yet reviewed, so it cannot be approved or paid.
        await ReadProblemAsync(ApproveAsync(Meera, claim), HttpStatusCode.Conflict);
        await ReadProblemAsync(PayAsync(Kiran, claim), HttpStatusCode.Conflict);

        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));
        await ReadProblemAsync(ReviewAsync(Meera, claim), HttpStatusCode.Conflict);   // already under review
    }

    [Fact]
    public async Task A_rejection_needs_a_reason_and_is_final()
    {
        var policy = await IssuePolicyAsync();
        var claim = await FileClaimAsync(policy.Id, 5_000m);
        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));

        var problem = await ReadProblemAsync(RejectAsync(Ravi, claim, "No"), HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty("reason", out _));

        claim = await ReadAsync<ClaimDto>(RejectAsync(Ravi, claim, "The damage happened before the policy started"));
        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.Equal("The damage happened before the policy started", claim.RejectionReason);
        Assert.Null(claim.ApprovedBy);

        await ReadProblemAsync(ApproveAsync(Kiran, claim), HttpStatusCode.Conflict);
        await ReadProblemAsync(PayAsync(Kiran, claim), HttpStatusCode.Conflict);

        // A rejected claim does not use up cover.
        Assert.Equal(policy.SumInsured, (await GetPolicyAsync(policy.Id)).RemainingCover);
    }

    [Fact]
    public async Task A_claim_needs_an_active_policy_and_an_incident_inside_the_term()
    {
        var policy = await IssuePolicyAsync();   // the term started 30 days ago

        await ReadProblemAsync(FileClaimAsync(Asha, policy.Id, 5_000m, Today.AddDays(-31)), HttpStatusCode.Conflict);
        await ReadProblemAsync(FileClaimAsync(Asha, policy.Id, 5_000m, policy.EndDate.AddDays(1)), HttpStatusCode.Conflict);
        await FileClaimAsync(policy.Id, 5_000m, policy.StartDate);   // the first day of the term is covered

        await ReadAsync<PolicyDto>(PostAsync(Asha, $"/api/policies/{policy.Id}/cancel"));
        await ReadProblemAsync(FileClaimAsync(Asha, policy.Id, 5_000m), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_invalid_claim_gets_400_with_field_errors()
    {
        var policy = await IssuePolicyAsync();
        var body = new { policyId = policy.Id, incidentDate = Today, amount = 0, description = "abc" };

        var problem = await ReadProblemAsync(PostAsync(Asha, "/api/claims", body), HttpStatusCode.BadRequest);

        var fields = problem.GetProperty("errors").EnumerateObject().Select(e => e.Name).Order();
        Assert.Equal(["amount", "description"], fields);
    }

    [Fact]
    public async Task Approve_without_a_row_version_gets_400()
    {
        var policy = await IssuePolicyAsync();
        var claim = await FileClaimAsync(policy.Id, 5_000m);
        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));

        var problem = await ReadProblemAsync(
            PostAsync(Meera, $"/api/claims/{claim.Id}/approve", new { }), HttpStatusCode.BadRequest);

        Assert.True(problem.GetProperty("errors").TryGetProperty("rowVersion", out _));
    }

    [Fact]
    public async Task Claims_are_filtered_by_status_and_policy()
    {
        var policy = await IssuePolicyAsync();
        var submitted = await FileClaimAsync(policy.Id, 1_000m);
        var reviewed = await FileClaimAsync(policy.Id, 2_000m);
        await ReadAsync<ClaimDto>(ReviewAsync(Ravi, reviewed));

        var ofPolicy = await ReadAsync<List<ClaimDto>>(Ravi.GetAsync($"/api/claims?policyId={policy.Id}"));
        var underReview = await ReadAsync<List<ClaimDto>>(Ravi.GetAsync($"/api/claims?status=UnderReview&policyId={policy.Id}"));
        var emptyFilters = await ReadAsync<List<ClaimDto>>(Ravi.GetAsync("/api/claims?status=&policyId="));

        Assert.Equal([reviewed.Id, submitted.Id], ofPolicy.Select(c => c.Id));   // newest first
        Assert.Equal([reviewed.Id], underReview.Select(c => c.Id));
        Assert.Contains(emptyFilters, c => c.Id == submitted.Id);                 // empty values mean "no filter"
    }

    [Fact]
    public async Task An_unknown_claim_gets_404()
    {
        await ReadProblemAsync(Asha.GetAsync("/api/claims/987654"), HttpStatusCode.NotFound);
        await ReadProblemAsync(PostAsync(Ravi, "/api/claims/987654/review"), HttpStatusCode.NotFound);
        await ReadProblemAsync(FileClaimAsync(Asha, policyId: 987_654, amount: 1_000m), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_trigger_handles_multi_row_statements_and_ignores_updates_that_keep_the_status()
    {
        var policy = await IssuePolicyAsync();
        var claims = new[]
        {
            await FileClaimAsync(policy.Id, 1_000m),
            await FileClaimAsync(policy.Id, 2_000m),
            await FileClaimAsync(policy.Id, 3_000m),
        };

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimDeskDbContext>();

        // One UPDATE statement that changes the status of three rows (2 is Ravi).
        var changed = await db.Database.ExecuteSqlAsync(
            $"UPDATE dbo.Claims SET Status = 'UnderReview', LastChangedByUserId = 2 WHERE PolicyId = {policy.Id}");
        Assert.Equal(3, changed);

        // An UPDATE that leaves the status alone must not be audited.
        await db.Database.ExecuteSqlAsync(
            $"UPDATE dbo.Claims SET Description = 'Edited after filing' WHERE PolicyId = {policy.Id}");

        foreach (var claim in claims)
        {
            var audit = await GetAuditAsync(claim.Id);

            Assert.Equal(2, audit.Count);
            Assert.Equal(ClaimStatus.Submitted, audit[1].OldStatus);
            Assert.Equal(ClaimStatus.UnderReview, audit[1].NewStatus);
            Assert.Equal("Ravi", audit[1].ChangedBy);
        }
    }
}
