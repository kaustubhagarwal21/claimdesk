using System.ComponentModel.DataAnnotations;
using ClaimDesk.Api.Domain;

namespace ClaimDesk.Api.Dtos;

// Body of both POST /api/policies/quote and POST /api/policies.
// Type and StartDate are nullable so that [Required] can tell "missing" from a default value.
public class PolicyRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A customer is required.")]
    public int CustomerId { get; set; }

    [Required]
    public PolicyType? Type { get; set; }

    [Range(10_000.0, 10_000_000.0, ErrorMessage = "Sum insured must be between 10000 and 10000000.")]
    public decimal SumInsured { get; set; }

    [Required]
    public DateOnly? StartDate { get; set; }
}

public record QuoteDto(PolicyType Type, decimal SumInsured, decimal RatePercent, decimal Premium);

public record PolicyDto(
    int Id,
    string PolicyNumber,
    int CustomerId,
    string CustomerName,
    PolicyType Type,
    decimal SumInsured,
    decimal Premium,
    DateOnly StartDate,
    DateOnly EndDate,
    PolicyStatus Status,
    decimal RemainingCover);
