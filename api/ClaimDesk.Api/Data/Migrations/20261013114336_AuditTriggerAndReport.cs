using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimDesk.Api.Data.Migrations
{
    /// <summary>
    /// Hand-written T-SQL for the database objects EF Core does not model:
    /// the audit trigger, the report procedure and a covering index.
    /// </summary>
    public partial class AuditTriggerAndReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Covers the remaining-cover query: SUM(Amount) of the Approved and Paid claims of one policy.
            migrationBuilder.Sql("""
                CREATE NONCLUSTERED INDEX IX_Claims_PolicyId_Status
                    ON dbo.Claims (PolicyId, Status)
                    INCLUDE (Amount);
                """);

            // Writes one audit row per claim whose status changed. It is set-based: "inserted" and
            // "deleted" hold every row of the statement, so a multi-row INSERT or UPDATE is handled too.
            // On INSERT there is no "deleted" row, which gives OldStatus = NULL for the first entry.
            migrationBuilder.Sql("""
                CREATE TRIGGER dbo.trg_Claims_StatusAudit
                ON dbo.Claims
                AFTER INSERT, UPDATE
                AS
                BEGIN
                    SET NOCOUNT ON;

                    INSERT INTO dbo.ClaimAudits (ClaimId, OldStatus, NewStatus, ChangedByUserId, ChangedAt)
                    SELECT i.Id, d.Status, i.Status, i.LastChangedByUserId, SYSUTCDATETIME()
                    FROM inserted AS i
                    LEFT JOIN deleted AS d ON d.Id = i.Id
                    WHERE d.Id IS NULL
                       OR d.Status <> i.Status;
                END
                """);

            // Claim count and total amount per policy type and claim status,
            // for incidents dated between @From and @To (both days included).
            migrationBuilder.Sql("""
                CREATE PROCEDURE dbo.usp_ClaimsSummary
                    @From date,
                    @To   date
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT p.[Type]      AS PolicyType,
                           c.[Status]    AS [Status],
                           COUNT(*)      AS ClaimCount,
                           SUM(c.Amount) AS TotalAmount
                    FROM dbo.Claims AS c
                    INNER JOIN dbo.Policies AS p ON p.Id = c.PolicyId
                    WHERE c.IncidentDate BETWEEN @From AND @To
                    GROUP BY p.[Type], c.[Status]
                    ORDER BY p.[Type], c.[Status];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_ClaimsSummary;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_Claims_StatusAudit;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Claims_PolicyId_Status ON dbo.Claims;");
        }
    }
}
