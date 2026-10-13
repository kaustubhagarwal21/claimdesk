using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Dtos;

// One result row of the stored procedure usp_ClaimsSummary.
// EF Core fills it by matching the property names to the column names the procedure returns.
public class ClaimsSummaryRow
{
    public string PolicyType { get; set; } = "";
    public string Status { get; set; } = "";
    public int ClaimCount { get; set; }

    [Precision(18, 2)]
    public decimal TotalAmount { get; set; }
}
