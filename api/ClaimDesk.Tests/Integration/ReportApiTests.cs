using System.Net;
using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;

namespace ClaimDesk.Tests.Integration;

// GET /api/reports/claims-summary, which runs the stored procedure usp_ClaimsSummary.
// This class has its own database, so the totals below are exact.
public class ReportApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task The_summary_groups_claims_by_policy_type_and_status()
    {
        var motor = await IssuePolicyAsync(500_000m, PolicyType.Motor);
        var health = await IssuePolicyAsync(300_000m, PolicyType.Health);

        // Motor: one Submitted, one Approved, one Paid, plus one older Submitted claim.
        await FileClaimAsync(motor.Id, 10_000m);
        await ApprovedClaimAsync(motor.Id, 25_000.50m);
        var paid = await ApprovedClaimAsync(motor.Id, 5_000m);
        await ReadAsync<ClaimDto>(PayAsync(Kiran, paid));
        await FileClaimAsync(motor.Id, 7_000m, incidentDate: Today.AddDays(-20));

        // Health: two Submitted and one Rejected.
        await FileClaimAsync(health.Id, 4_000m);
        await FileClaimAsync(health.Id, 6_000.25m);
        var rejected = await FileClaimAsync(health.Id, 3_000m);
        rejected = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, rejected));
        await ReadAsync<ClaimDto>(RejectAsync(Ravi, rejected, "Treatment is not covered"));

        // No dates: every claim. Rows come back ordered by policy type, then status.
        var all = await SummaryAsync("/api/reports/claims-summary");
        Assert.Equal(
            [
                ("Health", "Rejected", 1, 3_000m),
                ("Health", "Submitted", 2, 10_000.25m),
                ("Motor", "Approved", 1, 25_000.50m),
                ("Motor", "Paid", 1, 5_000m),
                ("Motor", "Submitted", 2, 17_000m),
            ],
            all);

        // The last ten days leave out the claim whose incident was 20 days ago.
        var recent = await SummaryAsync($"/api/reports/claims-summary?from={Today.AddDays(-10):yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        Assert.Contains(("Motor", "Submitted", 1, 10_000m), recent);
        Assert.Equal(6, recent.Sum(row => row.ClaimCount));

        // Only that older claim.
        var older = await SummaryAsync($"/api/reports/claims-summary?from={Today.AddDays(-25):yyyy-MM-dd}&to={Today.AddDays(-15):yyyy-MM-dd}");
        Assert.Equal([("Motor", "Submitted", 1, 7_000m)], older);
    }

    [Fact]
    public async Task A_range_without_claims_returns_an_empty_list()
    {
        var rows = await SummaryAsync("/api/reports/claims-summary?from=2001-01-01&to=2001-12-31");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task The_report_needs_a_user_and_valid_dates()
    {
        await ReadProblemAsync(Factory.CreateClient().GetAsync("/api/reports/claims-summary"), HttpStatusCode.Unauthorized);
        await ReadProblemAsync(Asha.GetAsync("/api/reports/claims-summary?from=yesterday"), HttpStatusCode.BadRequest);
    }

    private async Task<ClaimDto> ApprovedClaimAsync(int policyId, decimal amount)
    {
        var claim = await FileClaimAsync(policyId, amount);
        claim = await ReadAsync<ClaimDto>(ReviewAsync(Ravi, claim));
        return await ReadAsync<ClaimDto>(ApproveAsync(Meera, claim));
    }

    private async Task<List<(string PolicyType, string Status, int ClaimCount, decimal TotalAmount)>> SummaryAsync(string url)
    {
        var rows = await ReadAsync<List<ClaimsSummaryRow>>(Kiran.GetAsync(url));
        return rows.Select(row => (row.PolicyType, row.Status, row.ClaimCount, row.TotalAmount)).ToList();
    }
}
