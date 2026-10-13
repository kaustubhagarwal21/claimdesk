using System.Net;
using System.Text.Json;
using ClaimDesk.Api.Data;
using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimDesk.Tests.Integration;

// Users, customers, policies, identity and request validation.
public class PolicyApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    // ---- identity ----

    [Fact]
    public async Task Users_are_listed_without_the_header()
    {
        var users = await ReadAsync<List<UserDto>>(Factory.CreateClient().GetAsync("/api/users"));

        Assert.Equal(
            [new UserDto(1, "Asha", Role.Agent), new UserDto(2, "Ravi", Role.Adjuster),
             new UserDto(3, "Meera", Role.Adjuster), new UserDto(4, "Kiran", Role.Manager)],
            users);
    }

    [Theory]
    [InlineData(null)]     // no header
    [InlineData("99")]     // no such user
    [InlineData("asha")]   // not a number
    public async Task A_missing_or_unknown_user_gets_401(string? userId)
    {
        var client = Factory.CreateClient();
        if (userId is not null)
        {
            client.DefaultRequestHeaders.Add("X-User-Id", userId);
        }

        await ReadProblemAsync(client.GetAsync("/api/customers"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_OpenApi_document_is_served()
    {
        var response = await Factory.CreateClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- start-up seeding ----

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_rows()
    {
        await DbSeeder.MigrateAndSeedAsync(Factory.Services);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimDeskDbContext>();

        Assert.Equal(4, await db.Users.CountAsync());
        Assert.Equal(3, await db.Customers.CountAsync(c => c.Email.EndsWith("@example.com")));
    }

    // ---- customers ----

    [Fact]
    public async Task An_agent_creates_a_customer()
    {
        var body = new { name = "Dev Patel", email = "dev.patel@claimdesk.test" };

        var created = await ReadAsync<CustomerDto>(PostAsync(Asha, "/api/customers", body), HttpStatusCode.Created);

        Assert.True(created.Id > 0);
        Assert.Equal("Dev Patel", created.Name);

        var all = await ReadAsync<List<CustomerDto>>(Ravi.GetAsync("/api/customers"));
        Assert.Contains(created, all);
    }

    [Fact]
    public async Task A_second_customer_with_the_same_email_gets_409()
    {
        var body = new { name = "Sana Khan", email = "sana.khan@claimdesk.test" };
        await ReadAsync<CustomerDto>(PostAsync(Asha, "/api/customers", body), HttpStatusCode.Created);

        await ReadProblemAsync(PostAsync(Kiran, "/api/customers", body), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_invalid_customer_gets_400_with_field_errors()
    {
        var body = new { name = "A", email = "not-an-email" };

        var problem = await ReadProblemAsync(PostAsync(Asha, "/api/customers", body), HttpStatusCode.BadRequest);

        var errors = problem.GetProperty("errors");
        Assert.NotEmpty(errors.GetProperty("name").EnumerateArray());
        Assert.NotEmpty(errors.GetProperty("email").EnumerateArray());
    }

    // ---- policies ----

    [Theory]
    [InlineData(PolicyType.Motor, 400_000, 10_000)]    // 2.5%
    [InlineData(PolicyType.Health, 250_000, 10_000)]   // 4.0%
    [InlineData(PolicyType.Home, 1_500_000, 12_000)]   // 0.8%
    public async Task Issuing_a_policy_calculates_the_premium(PolicyType type, int sumInsured, int expectedPremium)
    {
        var startDate = Today.AddDays(-10);
        var body = new { customerId = await FirstCustomerIdAsync(), type, sumInsured, startDate };

        var response = await PostAsync(Asha, "/api/policies", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var policy = await ReadAsync<PolicyDto>(response);
        Assert.Equal(expectedPremium, policy.Premium);
        Assert.Matches(@"^POL-\d{6}$", policy.PolicyNumber);
        Assert.Equal(type, policy.Type);
        Assert.Equal(PolicyStatus.Active, policy.Status);
        Assert.Equal(startDate, policy.StartDate);
        Assert.Equal(startDate.AddYears(1).AddDays(-1), policy.EndDate);
        Assert.Equal(sumInsured, policy.RemainingCover);
        Assert.False(string.IsNullOrEmpty(policy.CustomerName));

        // The Location header points at the new policy.
        Assert.Equal(policy, await ReadAsync<PolicyDto>(Ravi.GetAsync(response.Headers.Location)));
    }

    [Fact]
    public async Task A_quote_returns_the_rate_and_premium_and_saves_nothing()
    {
        var customerId = await FirstCustomerIdAsync();
        var before = await ReadAsync<List<PolicyDto>>(Asha.GetAsync($"/api/policies?customerId={customerId}"));

        var body = new { customerId, type = "Health", sumInsured = 300_000, startDate = Today };
        var quote = await ReadAsync<QuoteDto>(PostAsync(Ravi, "/api/policies/quote", body));

        Assert.Equal(new QuoteDto(PolicyType.Health, 300_000m, 4.0m, 12_000m), quote);

        var after = await ReadAsync<List<PolicyDto>>(Asha.GetAsync($"/api/policies?customerId={customerId}"));
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task An_adjuster_cannot_issue_or_cancel_a_policy_or_create_a_customer()
    {
        var policy = await IssuePolicyAsync();
        var body = new { customerId = policy.CustomerId, type = "Motor", sumInsured = 400_000, startDate = Today };

        await ReadProblemAsync(PostAsync(Ravi, "/api/policies", body), HttpStatusCode.Forbidden);
        await ReadProblemAsync(PostAsync(Ravi, $"/api/policies/{policy.Id}/cancel"), HttpStatusCode.Forbidden);
        await ReadProblemAsync(
            PostAsync(Ravi, "/api/customers", new { name = "Not Allowed", email = "not.allowed@claimdesk.test" }),
            HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_policy_is_cancelled_once()
    {
        var policy = await IssuePolicyAsync();

        var cancelled = await ReadAsync<PolicyDto>(PostAsync(Kiran, $"/api/policies/{policy.Id}/cancel"));
        Assert.Equal(PolicyStatus.Cancelled, cancelled.Status);

        await ReadProblemAsync(PostAsync(Asha, $"/api/policies/{policy.Id}/cancel"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_policy_past_its_term_is_listed_as_Expired()
    {
        var body = new { customerId = await FirstCustomerIdAsync(), type = "Home", sumInsured = 900_000, startDate = Today.AddYears(-2) };

        var issued = await ReadAsync<PolicyDto>(PostAsync(Asha, "/api/policies", body), HttpStatusCode.Created);

        Assert.Equal(PolicyStatus.Expired, (await GetPolicyAsync(issued.Id)).Status);
        await ReadProblemAsync(FileClaimAsync(Asha, issued.Id, 1_000m, Today.AddYears(-2).AddDays(5)), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Policies_are_filtered_by_status_and_customer()
    {
        var customer = await ReadAsync<CustomerDto>(
            PostAsync(Asha, "/api/customers", new { name = "Filter Test", email = "filter.test@claimdesk.test" }),
            HttpStatusCode.Created);
        var body = new { customerId = customer.Id, type = "Motor", sumInsured = 200_000, startDate = Today };
        var active = await ReadAsync<PolicyDto>(PostAsync(Asha, "/api/policies", body), HttpStatusCode.Created);
        var cancelled = await ReadAsync<PolicyDto>(PostAsync(Asha, "/api/policies", body), HttpStatusCode.Created);
        await ReadAsync<PolicyDto>(PostAsync(Asha, $"/api/policies/{cancelled.Id}/cancel"));

        var ofCustomer = await ReadAsync<List<PolicyDto>>(Asha.GetAsync($"/api/policies?customerId={customer.Id}"));
        var activeOnly = await ReadAsync<List<PolicyDto>>(Asha.GetAsync($"/api/policies?status=Active&customerId={customer.Id}"));
        var emptyFilters = await ReadAsync<List<PolicyDto>>(Asha.GetAsync("/api/policies?status=&customerId="));

        Assert.Equal([cancelled.Id, active.Id], ofCustomer.Select(p => p.Id));   // newest first
        Assert.Equal([active.Id], activeOnly.Select(p => p.Id));
        Assert.Contains(emptyFilters, p => p.Id == active.Id);                    // empty values mean "no filter"
        await ReadProblemAsync(Asha.GetAsync("/api/policies?status=Lapsed"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_policy_or_customer_gets_404()
    {
        var body = new { customerId = 987_654, type = "Motor", sumInsured = 400_000, startDate = Today };

        await ReadProblemAsync(Asha.GetAsync("/api/policies/987654"), HttpStatusCode.NotFound);
        await ReadProblemAsync(PostAsync(Asha, "/api/policies/987654/cancel"), HttpStatusCode.NotFound);
        await ReadProblemAsync(PostAsync(Asha, "/api/policies", body), HttpStatusCode.NotFound);
        await ReadProblemAsync(Asha.GetAsync("/api/no-such-route"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_invalid_policy_gets_400_with_field_errors()
    {
        // The sum insured is below the minimum; type and startDate are missing.
        var body = new { customerId = await FirstCustomerIdAsync(), sumInsured = 5_000 };

        var problem = await ReadProblemAsync(PostAsync(Asha, "/api/policies", body), HttpStatusCode.BadRequest);

        var fields = problem.GetProperty("errors").EnumerateObject().Select(e => e.Name).Order();
        Assert.Equal(["startDate", "sumInsured", "type"], fields);
    }

    [Fact]
    public async Task An_unknown_policy_type_gets_400()
    {
        var body = new { customerId = await FirstCustomerIdAsync(), type = "Boat", sumInsured = 400_000, startDate = Today };

        var problem = await ReadProblemAsync(PostAsync(Asha, "/api/policies/quote", body), HttpStatusCode.BadRequest);

        Assert.Equal(JsonValueKind.Object, problem.GetProperty("errors").ValueKind);
    }
}
