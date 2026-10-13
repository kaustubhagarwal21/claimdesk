using ClaimDesk.Api.Data;
using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Services;

public class PolicyService(ClaimDeskDbContext db)
{
    public async Task<QuoteDto> QuoteAsync(PolicyRequest request)
    {
        await EnsureCustomerExistsAsync(request.CustomerId);

        var type = request.Type!.Value;
        return new QuoteDto(
            type,
            request.SumInsured,
            PremiumCalculator.RatePercent(type),
            PremiumCalculator.Calculate(type, request.SumInsured));
    }

    public async Task<PolicyDto> IssueAsync(PolicyRequest request, User user)
    {
        RoleGuard.Require(user.Role, Role.Agent, Role.Manager);
        await EnsureCustomerExistsAsync(request.CustomerId);

        var type = request.Type!.Value;
        var startDate = request.StartDate!.Value;

        var policy = new Policy
        {
            CustomerId = request.CustomerId,
            Type = type,
            SumInsured = request.SumInsured,
            Premium = PremiumCalculator.Calculate(type, request.SumInsured),
            StartDate = startDate,
            EndDate = startDate.AddYears(1).AddDays(-1),   // one-year term, both days included
            Status = PolicyStatus.Active,
        };

        db.Policies.Add(policy);
        await db.SaveChangesAsync();

        return await GetAsync(policy.Id);
    }

    public async Task<List<PolicyDto>> ListAsync(PolicyStatus? status, int? customerId)
    {
        await ExpireEndedPoliciesAsync();

        var policies = db.Policies.AsQueryable();
        if (status is not null)
        {
            policies = policies.Where(p => p.Status == status);
        }
        if (customerId is not null)
        {
            policies = policies.Where(p => p.CustomerId == customerId);
        }

        return await ToDto(policies.OrderByDescending(p => p.Id)).ToListAsync();
    }

    public async Task<PolicyDto> GetAsync(int id)
    {
        await ExpireEndedPoliciesAsync();

        return await ToDto(db.Policies.Where(p => p.Id == id)).FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Policy {id} was not found.");
    }

    public async Task<PolicyDto> CancelAsync(int id, User user)
    {
        RoleGuard.Require(user.Role, Role.Agent, Role.Manager);
        await ExpireEndedPoliciesAsync();

        var policy = await db.Policies.FindAsync(id)
            ?? throw new NotFoundException($"Policy {id} was not found.");

        if (policy.Status != PolicyStatus.Active)
        {
            throw new BusinessRuleException(
                $"Only an Active policy can be cancelled; {policy.PolicyNumber} is {policy.Status}.");
        }

        policy.Status = PolicyStatus.Cancelled;
        await db.SaveChangesAsync();

        return await GetAsync(id);
    }

    // Remaining cover = sum insured minus the Approved and Paid claims on the policy.
    // The whole projection runs in SQL.
    private static IQueryable<PolicyDto> ToDto(IQueryable<Policy> policies) =>
        policies.Select(p => new PolicyDto(
            p.Id,
            p.PolicyNumber,
            p.CustomerId,
            p.Customer.Name,
            p.Type,
            p.SumInsured,
            p.Premium,
            p.StartDate,
            p.EndDate,
            p.Status,
            p.SumInsured - p.Claims
                .Where(c => c.Status == ClaimStatus.Approved || c.Status == ClaimStatus.Paid)
                .Sum(c => c.Amount)));

    // A policy past its end date becomes Expired the next time policies are read.
    // One UPDATE statement; a scheduled job would do this in a larger system.
    private Task ExpireEndedPoliciesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return db.Policies
            .Where(p => p.Status == PolicyStatus.Active && p.EndDate < today)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.Status, PolicyStatus.Expired));
    }

    private async Task EnsureCustomerExistsAsync(int customerId)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId))
        {
            throw new NotFoundException($"Customer {customerId} was not found.");
        }
    }
}
