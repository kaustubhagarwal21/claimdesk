namespace ClaimDesk.Api.Domain;

public class Policy
{
    public int Id { get; set; }

    // Computed by SQL Server from Id, for example POL-000001.
    public string PolicyNumber { get; set; } = "";

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public PolicyType Type { get; set; }
    public decimal SumInsured { get; set; }
    public decimal Premium { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PolicyStatus Status { get; set; }

    public List<Claim> Claims { get; set; } = [];
}
