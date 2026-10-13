using ClaimDesk.Api.Domain;
using ClaimDesk.Api.Dtos;
using ClaimDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaimDesk.Api.Controllers;

[Route("api/claims")]
public class ClaimsController(ClaimService claims) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ClaimDto>> FileClaim(FileClaimRequest request)
    {
        var claim = await claims.FileAsync(request, CurrentUser);
        return CreatedAtAction(nameof(Get), new { id = claim.Id }, claim);
    }

    [HttpGet]
    public Task<List<ClaimDto>> List([FromQuery] ClaimStatus? status, [FromQuery] int? policyId) =>
        claims.ListAsync(status, policyId);

    [HttpGet("{id:int}")]
    public Task<ClaimDetailDto> Get(int id) => claims.GetAsync(id);

    [HttpPost("{id:int}/review")]
    public Task<ClaimDto> Review(int id) => claims.ReviewAsync(id, CurrentUser);

    [HttpPost("{id:int}/approve")]
    public Task<ClaimDto> Approve(int id, RowVersionRequest request) =>
        claims.ApproveAsync(id, request, CurrentUser);

    [HttpPost("{id:int}/reject")]
    public Task<ClaimDto> Reject(int id, RejectClaimRequest request) =>
        claims.RejectAsync(id, request, CurrentUser);

    [HttpPost("{id:int}/pay")]
    public Task<ClaimDto> Pay(int id, RowVersionRequest request) =>
        claims.PayAsync(id, request, CurrentUser);
}
