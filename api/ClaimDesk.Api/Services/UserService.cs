using ClaimDesk.Api.Data;
using ClaimDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Services;

public class UserService(ClaimDeskDbContext db)
{
    public Task<List<UserDto>> ListAsync() =>
        db.Users
            .OrderBy(u => u.Id)
            .Select(u => new UserDto(u.Id, u.Name, u.Role))
            .ToListAsync();
}
