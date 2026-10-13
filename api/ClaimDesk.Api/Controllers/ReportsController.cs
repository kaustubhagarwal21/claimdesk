using ClaimDesk.Api.Dtos;
using ClaimDesk.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaimDesk.Api.Controllers;

[Route("api/reports")]
public class ReportsController(ReportService reports) : ApiControllerBase
{
    // Claims grouped by policy type and status, for incidents dated from..to (both optional).
    [HttpGet("claims-summary")]
    public Task<List<ClaimsSummaryRow>> ClaimsSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        reports.ClaimsSummaryAsync(from, to);
}
