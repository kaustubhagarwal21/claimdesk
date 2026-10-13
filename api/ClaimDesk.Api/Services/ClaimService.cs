using System.Linq.Expressions;
using ClaimDesk.Api.Data;
using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Services;

public class ClaimService(ClaimDeskDbContext db, PolicyService policies)
{
    public async Task<ClaimDto> FileAsync(FileClaimRequest request, User user)
    {
        ClaimWorkflow.EnsureRoleCanSet(user.Role, ClaimStatus.Submitted);

        var policy = await policies.GetAsync(request.PolicyId);
        var incidentDate = request.IncidentDate!.Value;
        ClaimWorkflow.EnsureCanFile(
            policy.Status, policy.StartDate, policy.EndDate, incidentDate, request.Amount, policy.RemainingCover);

        var claim = new Claim
        {
            PolicyId = policy.Id,
            IncidentDate = incidentDate,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Status = ClaimStatus.Submitted,
            FiledByUserId = user.Id,
            LastChangedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
        };

        db.Claims.Add(claim);
        await db.SaveChangesAsync();   // the trigger writes the first audit row

        return await GetDtoAsync(claim.Id);
    }

    public Task<List<ClaimDto>> ListAsync(ClaimStatus? status, int? policyId)
    {
        var claims = db.Claims.AsQueryable();
        if (status is not null)
        {
            claims = claims.Where(c => c.Status == status);
        }
        if (policyId is not null)
        {
            claims = claims.Where(c => c.PolicyId == policyId);
        }

        return claims.OrderByDescending(c => c.Id).Select(ToDto).ToListAsync();
    }

    public async Task<ClaimDetailDto> GetAsync(int id)
    {
        var claim = await GetDtoAsync(id);

        var audit = await db.ClaimAudits
            .Where(a => a.ClaimId == id)
            .OrderBy(a => a.Id)
            .Select(a => new AuditEntryDto(a.OldStatus, a.NewStatus, a.ChangedBy.Name, a.ChangedAt))
            .ToListAsync();

        return new ClaimDetailDto(claim) { Audit = audit };
    }

    public async Task<ClaimDto> ReviewAsync(int id, User user)
    {
        var claim = await FindAsync(id);
        ClaimWorkflow.EnsureCanMove(user.Role, claim.Status, ClaimStatus.UnderReview);

        claim.ReviewedByUserId = user.Id;
        return await SaveStatusAsync(claim, ClaimStatus.UnderReview, user);
    }

    public async Task<ClaimDto> ApproveAsync(int id, RowVersionRequest request, User user)
    {
        var claim = await FindAsync(id);
        ClaimWorkflow.EnsureCanMove(user.Role, claim.Status, ClaimStatus.Approved);

        var policy = await policies.GetAsync(claim.PolicyId);
        ClaimWorkflow.EnsureCanApprove(user.Id, user.Role, claim.ReviewedByUserId, claim.Amount, policy.RemainingCover);

        claim.ApprovedByUserId = user.Id;
        return await SaveStatusAsync(claim, ClaimStatus.Approved, user, request.RowVersion);
    }

    public async Task<ClaimDto> RejectAsync(int id, RejectClaimRequest request, User user)
    {
        var claim = await FindAsync(id);
        ClaimWorkflow.EnsureCanMove(user.Role, claim.Status, ClaimStatus.Rejected);

        claim.RejectionReason = request.Reason.Trim();
        return await SaveStatusAsync(claim, ClaimStatus.Rejected, user, request.RowVersion);
    }

    public async Task<ClaimDto> PayAsync(int id, RowVersionRequest request, User user)
    {
        var claim = await FindAsync(id);
        ClaimWorkflow.EnsureCanMove(user.Role, claim.Status, ClaimStatus.Paid);

        return await SaveStatusAsync(claim, ClaimStatus.Paid, user, request.RowVersion);
    }

    private async Task<ClaimDto> SaveStatusAsync(Claim claim, ClaimStatus newStatus, User user, byte[]? rowVersion = null)
    {
        claim.Status = newStatus;
        claim.LastChangedByUserId = user.Id;   // read by the audit trigger

        // EF Core adds "AND RowVersion = @original" to the UPDATE. Using the value the client
        // last read means a stale copy matches no row, and SaveChanges throws
        // DbUpdateConcurrencyException, which the API returns as 409.
        if (rowVersion is not null)
        {
            db.Entry(claim).Property(c => c.RowVersion).OriginalValue = rowVersion;
        }

        await db.SaveChangesAsync();

        return await GetDtoAsync(claim.Id);
    }

    private async Task<Claim> FindAsync(int id) =>
        await db.Claims.FindAsync(id) ?? throw new NotFoundException($"Claim {id} was not found.");

    private async Task<ClaimDto> GetDtoAsync(int id) =>
        await db.Claims.Where(c => c.Id == id).Select(ToDto).FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Claim {id} was not found.");

    // Used inside queries, so EF Core translates it to SQL with the joins it needs.
    private static readonly Expression<Func<Claim, ClaimDto>> ToDto = c => new ClaimDto
    {
        Id = c.Id,
        ClaimNumber = c.ClaimNumber,
        PolicyId = c.PolicyId,
        PolicyNumber = c.Policy.PolicyNumber,
        PolicyType = c.Policy.Type,
        IncidentDate = c.IncidentDate,
        Amount = c.Amount,
        Description = c.Description,
        Status = c.Status,
        FiledBy = c.FiledBy.Name,
        ReviewedBy = c.ReviewedBy!.Name,
        ApprovedBy = c.ApprovedBy!.Name,
        RejectionReason = c.RejectionReason,
        CreatedAt = c.CreatedAt,
        RowVersion = c.RowVersion,
    };
}
