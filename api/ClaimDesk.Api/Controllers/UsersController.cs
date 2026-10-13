using ClaimDesk.Api.Dtos;
using ClaimDesk.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimDesk.Api.Controllers;

[Route("api/users")]
public class UsersController(UserService users) : ApiControllerBase
{
    // The front end calls this to fill its "act as" user picker, so no header is needed.
    [HttpGet]
    [AllowAnonymous]
    public Task<List<UserDto>> List() => users.ListAsync();
}
