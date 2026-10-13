namespace ClaimDesk.Api.Domain;

// One row per status change of a claim.
// Rows are written only by the trigger trg_Claims_StatusAudit; the application just reads them.
public class ClaimAudit
{
    public int Id { get; set; }

    public int ClaimId { get; set; }
    public Claim Claim { get; set; } = null!;

    public ClaimStatus? OldStatus { get; set; }   // null for the row written when the claim is filed
    public ClaimStatus NewStatus { get; set; }

    public int ChangedByUserId { get; set; }
    public User ChangedBy { get; set; } = null!;

    public DateTime ChangedAt { get; set; }
}
