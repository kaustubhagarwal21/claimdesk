namespace ClaimDesk.Api.Domain;

// The claim rules in one place, with no database access, so they can be unit tested.
//
//   Submitted -> UnderReview -> Approved -> Paid
//                            -> Rejected
public static class ClaimWorkflow
{
    // A claim above this amount can be approved only by a Manager.
    public const decimal ManagerApprovalThreshold = 100_000m;

    private static readonly Dictionary<ClaimStatus, ClaimStatus[]> NextStatuses = new()
    {
        [ClaimStatus.Submitted] = [ClaimStatus.UnderReview],
        [ClaimStatus.UnderReview] = [ClaimStatus.Approved, ClaimStatus.Rejected],
        [ClaimStatus.Approved] = [ClaimStatus.Paid],
        [ClaimStatus.Rejected] = [],
        [ClaimStatus.Paid] = [],
    };

    // The roles that may put a claim into each status.
    private static readonly Dictionary<ClaimStatus, Role[]> RolesThatCanSet = new()
    {
        [ClaimStatus.Submitted] = [Role.Agent],
        [ClaimStatus.UnderReview] = [Role.Adjuster],
        [ClaimStatus.Approved] = [Role.Adjuster, Role.Manager],
        [ClaimStatus.Rejected] = [Role.Adjuster, Role.Manager],
        [ClaimStatus.Paid] = [Role.Manager],
    };

    public static bool CanTransition(ClaimStatus from, ClaimStatus to) => NextStatuses[from].Contains(to);

    public static void EnsureRoleCanSet(Role role, ClaimStatus status) =>
        RoleGuard.Require(role, RolesThatCanSet[status]);

    // Role check first (403), then the transition itself (409).
    public static void EnsureCanMove(Role role, ClaimStatus from, ClaimStatus to)
    {
        EnsureRoleCanSet(role, to);

        if (!CanTransition(from, to))
        {
            throw new BusinessRuleException($"A claim cannot move from {from} to {to}.");
        }
    }

    public static void EnsureCanFile(
        PolicyStatus policyStatus, DateOnly termStart, DateOnly termEnd,
        DateOnly incidentDate, decimal amount, decimal remainingCover)
    {
        if (policyStatus != PolicyStatus.Active)
        {
            throw new BusinessRuleException($"A claim can be filed only on an Active policy; this policy is {policyStatus}.");
        }

        if (incidentDate < termStart || incidentDate > termEnd)
        {
            throw new BusinessRuleException(
                $"The incident date must be inside the policy term ({termStart:yyyy-MM-dd} to {termEnd:yyyy-MM-dd}).");
        }

        EnsureWithinCover(amount, remainingCover);
    }

    public static void EnsureCanApprove(
        int approverId, Role approverRole, int? reviewedByUserId, decimal amount, decimal remainingCover)
    {
        if (amount > ManagerApprovalThreshold && approverRole != Role.Manager)
        {
            throw new ForbiddenException($"A claim above {ManagerApprovalThreshold:0} can be approved only by a Manager.");
        }

        // Maker-checker: one person reviews, a different person approves.
        if (reviewedByUserId == approverId)
        {
            throw new BusinessRuleException("The user who reviewed a claim cannot also approve it.");
        }

        // Checked again here because other claims on the policy may have been approved since filing.
        EnsureWithinCover(amount, remainingCover);
    }

    private static void EnsureWithinCover(decimal amount, decimal remainingCover)
    {
        if (amount > remainingCover)
        {
            throw new BusinessRuleException(
                $"The claim amount {amount:0.00} is above the remaining cover {remainingCover:0.00}.");
        }
    }
}
