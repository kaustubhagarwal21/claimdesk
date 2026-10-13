using ClaimDesk.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Data;

public static class DbSeeder
{
    // Safe to run on every start: migrations already applied are skipped
    // and rows are added only to empty tables.
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimDeskDbContext>();

        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            db.Users.AddRange(
                new User { Id = 1, Name = "Asha", Role = Role.Agent },
                new User { Id = 2, Name = "Ravi", Role = Role.Adjuster },
                new User { Id = 3, Name = "Meera", Role = Role.Adjuster },
                new User { Id = 4, Name = "Kiran", Role = Role.Manager });
        }

        if (!await db.Customers.AnyAsync())
        {
            db.Customers.AddRange(
                new Customer { Name = "Neha Sharma", Email = "neha.sharma@example.com" },
                new Customer { Name = "Arjun Mehta", Email = "arjun.mehta@example.com" },
                new Customer { Name = "Priya Nair", Email = "priya.nair@example.com" });
        }

        await db.SaveChangesAsync();
    }
}
