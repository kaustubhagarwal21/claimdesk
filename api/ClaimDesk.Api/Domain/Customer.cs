namespace ClaimDesk.Api.Domain;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";

    public List<Policy> Policies { get; set; } = [];
}
