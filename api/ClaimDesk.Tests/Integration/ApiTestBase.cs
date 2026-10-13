using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;

namespace ClaimDesk.Tests.Integration;

// Shared helpers: one HttpClient per seeded user and short methods for the common calls.
public abstract class ApiTestBase(ApiFactory factory) : IClassFixture<ApiFactory>
{
    protected static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient Asha { get; } = factory.ClientFor(1);    // Agent
    protected HttpClient Ravi { get; } = factory.ClientFor(2);    // Adjuster
    protected HttpClient Meera { get; } = factory.ClientFor(3);   // Adjuster
    protected HttpClient Kiran { get; } = factory.ClientFor(4);   // Manager

    protected static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body = null) =>
        body is null ? client.PostAsync(url, null) : client.PostAsJsonAsync(url, body, ApiFactory.Json);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;

    // Asserts the status code and reads the body in one step.
    protected static async Task<T> ReadAsync<T>(Task<HttpResponseMessage> call, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await call;
        Assert.Equal(expected, response.StatusCode);
        return await ReadAsync<T>(response);
    }

    // Asserts an RFC 7807 error response and returns its body.
    protected static async Task<JsonElement> ReadProblemAsync(Task<HttpResponseMessage> call, HttpStatusCode expected)
    {
        var response = await call;
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await ReadAsync<JsonElement>(response);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("title").GetString()));
        return problem;
    }

    protected async Task<int> FirstCustomerIdAsync() =>
        (await ReadAsync<List<CustomerDto>>(Asha.GetAsync("/api/customers")))[0].Id;

    // The term starts 30 days ago, so the policy is Active and recent incident dates are inside it.
    protected async Task<PolicyDto> IssuePolicyAsync(decimal sumInsured = 500_000m, PolicyType type = PolicyType.Motor)
    {
        var body = new { customerId = await FirstCustomerIdAsync(), type, sumInsured, startDate = Today.AddDays(-30) };
        return await ReadAsync<PolicyDto>(PostAsync(Asha, "/api/policies", body), HttpStatusCode.Created);
    }

    protected Task<PolicyDto> GetPolicyAsync(int id) => ReadAsync<PolicyDto>(Asha.GetAsync($"/api/policies/{id}"));

    protected static Task<HttpResponseMessage> FileClaimAsync(
        HttpClient client, int policyId, decimal amount, DateOnly? incidentDate = null) =>
        PostAsync(client, "/api/claims", new
        {
            policyId,
            incidentDate = incidentDate ?? Today.AddDays(-3),
            amount,
            description = "Windscreen cracked by a stone on the highway",
        });

    protected Task<ClaimDto> FileClaimAsync(int policyId, decimal amount, DateOnly? incidentDate = null) =>
        ReadAsync<ClaimDto>(FileClaimAsync(Asha, policyId, amount, incidentDate), HttpStatusCode.Created);

    protected static Task<HttpResponseMessage> ReviewAsync(HttpClient client, ClaimDto claim) =>
        PostAsync(client, $"/api/claims/{claim.Id}/review");

    protected static Task<HttpResponseMessage> ApproveAsync(HttpClient client, ClaimDto claim) =>
        PostAsync(client, $"/api/claims/{claim.Id}/approve", new { rowVersion = claim.RowVersion });

    protected static Task<HttpResponseMessage> RejectAsync(HttpClient client, ClaimDto claim, string reason) =>
        PostAsync(client, $"/api/claims/{claim.Id}/reject", new { reason, rowVersion = claim.RowVersion });

    protected static Task<HttpResponseMessage> PayAsync(HttpClient client, ClaimDto claim) =>
        PostAsync(client, $"/api/claims/{claim.Id}/pay", new { rowVersion = claim.RowVersion });

    protected Task<ClaimDto> GetClaimAsync(int id) => ReadAsync<ClaimDto>(Asha.GetAsync($"/api/claims/{id}"));

    protected async Task<List<AuditEntryDto>> GetAuditAsync(int claimId) =>
        (await ReadAsync<AuditTrail>(Asha.GetAsync($"/api/claims/{claimId}"))).Audit;

    // The "audit" part of the ClaimDetail response.
    private record AuditTrail(List<AuditEntryDto> Audit);
}
