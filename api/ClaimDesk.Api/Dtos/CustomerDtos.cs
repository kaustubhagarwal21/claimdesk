using System.ComponentModel.DataAnnotations;

namespace ClaimDesk.Api.Dtos;

public record CustomerDto(int Id, string Name, string Email);

public class CreateCustomerRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";
}
