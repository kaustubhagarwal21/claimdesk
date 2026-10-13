namespace ClaimDesk.Api.Domain;

public class Claim
{
    public int Id { get; set; }

    // Computed by SQL Server from Id, for example CLM-000001.
    public string ClaimNumber { get; set; } = "";

    public int PolicyId { get; set; }
    public Policy Policy { get; set; } = null!;

    public DateOnly IncidentDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public ClaimStatus Status { get; set; }

    public int FiledByUserId { get; set; }
    public User FiledBy { get; set; } = null!;

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedBy { get; set; }

    public int? ApprovedByUserId { get; set; }
    public User? ApprovedBy { get; set; }

    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }

    // The user behind the latest change. The audit trigger copies it into ClaimAudits.
    public int LastChangedByUserId { get; set; }

    // SQL Server rowversion: the database changes it on every update.
    // It is the optimistic concurrency token for approve, reject and pay.
    public byte[] RowVersion { get; set; } = [];

    public List<ClaimAudit> Audits { get; set; } = [];
}
