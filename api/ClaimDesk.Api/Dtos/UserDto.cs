using ClaimDesk.Api.Domain;

namespace ClaimDesk.Api.Dtos;

public record UserDto(int Id, string Name, Role Role);
