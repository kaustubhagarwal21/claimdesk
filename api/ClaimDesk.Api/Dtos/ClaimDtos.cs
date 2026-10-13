using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClaimDesk.Api.Domain;

namespace ClaimDesk.Api.Dtos;

public class FileClaimRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A policy is required.")]
    public int PolicyId { get; set; }

    [Required]
    public DateOnly? IncidentDate { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required, StringLength(500, MinimumLength = 5)]
    public string Description { get; set; } = "";
}

// Body of approve and pay. JSON carries the byte[] as base64.
public class RowVersionRequest
{
    [Required]
    public byte[]? RowVersion { get; set; }
}

public class RejectClaimRequest : RowVersionRequest
{
    [Required, StringLength(300, MinimumLength = 5)]
    public string Reason { get; set; } = "";
}

public record ClaimDto
{
    public int Id { get; init; }
    public string ClaimNumber { get; init; } = "";
    public int PolicyId { get; init; }
    public string PolicyNumber { get; init; } = "";
    public PolicyType PolicyType { get; init; }
    public DateOnly IncidentDate { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = "";
    public ClaimStatus Status { get; init; }
    public string FiledBy { get; init; } = "";
    public string? ReviewedBy { get; init; }
    public string? ApprovedBy { get; init; }
    public string? RejectionReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public byte[] RowVersion { get; init; } = [];
}

// A claim plus its audit trail. The constructor copies every ClaimDto property.
public record ClaimDetailDto : ClaimDto
{
    public ClaimDetailDto(ClaimDto claim) : base(claim)
    {
    }

    [JsonPropertyOrder(1)]   // after the claim fields in the JSON
    public List<AuditEntryDto> Audit { get; init; } = [];
}

public record AuditEntryDto(ClaimStatus? OldStatus, ClaimStatus NewStatus, string ChangedBy, DateTime ChangedAt);
