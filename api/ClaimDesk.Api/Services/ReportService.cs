using ClaimDesk.Api.Data;
using ClaimDesk.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Services;

public class ReportService(ClaimDeskDbContext db)
{
    // Calls the stored procedure usp_ClaimsSummary. The interpolated values are sent
    // as SQL parameters, not pasted into the SQL text. A missing bound means "no limit".
    public Task<List<ClaimsSummaryRow>> ClaimsSummaryAsync(DateOnly? from, DateOnly? to)
    {
        var fromDate = from ?? DateOnly.MinValue;
        var toDate = to ?? DateOnly.MaxValue;

        return db.Database
            .SqlQuery<ClaimsSummaryRow>($"EXEC dbo.usp_ClaimsSummary @From = {fromDate}, @To = {toDate}")
            .ToListAsync();
    }
}
