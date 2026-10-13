using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ClaimDesk.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    // Put into HttpContext.Items by CurrentUserFilter before the action runs.
    protected User CurrentUser => (User)HttpContext.Items[CurrentUserFilter.ItemKey]!;
}
