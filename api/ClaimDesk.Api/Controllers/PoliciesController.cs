using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;
using ClaimDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaimDesk.Api.Controllers;

[Route("api/policies")]
public class PoliciesController(PolicyService policies) : ApiControllerBase
{
    [HttpPost("quote")]
    public Task<QuoteDto> Quote(PolicyRequest request) => policies.QuoteAsync(request);

    [HttpPost]
    public async Task<ActionResult<PolicyDto>> Issue(PolicyRequest request)
    {
        var policy = await policies.IssueAsync(request, CurrentUser);
        return CreatedAtAction(nameof(Get), new { id = policy.Id }, policy);
    }

    [HttpGet]
    public Task<List<PolicyDto>> List([FromQuery] PolicyStatus? status, [FromQuery] int? customerId) =>
        policies.ListAsync(status, customerId);

    [HttpGet("{id:int}")]
    public Task<PolicyDto> Get(int id) => policies.GetAsync(id);

    [HttpPost("{id:int}/cancel")]
    public Task<PolicyDto> Cancel(int id) => policies.CancelAsync(id, CurrentUser);
}
