using ClaimDesk.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ClaimDesk.Api.Data;

public class ClaimDeskDbContext(DbContextOptions<ClaimDeskDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimAudit> ClaimAudits => Set<ClaimAudit>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored by name so the trigger, the report and ad-hoc SQL stay readable.
        configurationBuilder.Properties<Role>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<PolicyType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<PolicyStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ClaimStatus>().HaveConversion<string>().HaveMaxLength(20);

        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            // Ids are fixed (1 to 4) because the demo front end sends them in X-User-Id.
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Customer>(customer =>
        {
            customer.Property(c => c.Name).HasMaxLength(100);
            customer.Property(c => c.Email).HasMaxLength(200);
            customer.HasIndex(c => c.Email).IsUnique();
        });

        modelBuilder.Entity<Policy>(policy =>
        {
            policy.Property(p => p.PolicyNumber)
                .HasMaxLength(10)
                .HasComputedColumnSql("'POL-' + RIGHT('000000' + CAST([Id] AS varchar(10)), 6)", stored: true);
            policy.HasIndex(p => p.PolicyNumber).IsUnique();
        });

        modelBuilder.Entity<Claim>(claim =>
        {
            // Tells EF Core the table has a trigger, so it does not use the OUTPUT clause when saving.
            claim.ToTable(table => table.HasTrigger("trg_Claims_StatusAudit"));

            claim.Property(c => c.ClaimNumber)
                .HasMaxLength(10)
                .HasComputedColumnSql("'CLM-' + RIGHT('000000' + CAST([Id] AS varchar(10)), 6)", stored: true);
            claim.HasIndex(c => c.ClaimNumber).IsUnique();

            claim.Property(c => c.Description).HasMaxLength(500);
            claim.Property(c => c.RejectionReason).HasMaxLength(300);
            claim.Property(c => c.RowVersion).IsRowVersion();

            claim.HasOne(c => c.FiledBy).WithMany().HasForeignKey(c => c.FiledByUserId);
            claim.HasOne(c => c.ReviewedBy).WithMany().HasForeignKey(c => c.ReviewedByUserId);
            claim.HasOne(c => c.ApprovedBy).WithMany().HasForeignKey(c => c.ApprovedByUserId);
            claim.HasOne<User>().WithMany().HasForeignKey(c => c.LastChangedByUserId);
        });

        modelBuilder.Entity<ClaimAudit>(audit =>
        {
            audit.HasOne(a => a.Claim).WithMany(c => c.Audits).HasForeignKey(a => a.ClaimId);
            audit.HasOne(a => a.ChangedBy).WithMany().HasForeignKey(a => a.ChangedByUserId);
        });

        // Nothing is ever deleted here, and SQL Server refuses several cascade paths to Users anyway.
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }
}

// SQL Server datetime2 has no time zone. All timestamps are saved in UTC,
// so mark them as UTC when they are read back and JSON gets the trailing "Z".
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
