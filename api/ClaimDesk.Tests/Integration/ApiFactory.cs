using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimDesk.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimDesk.Tests.Integration;

// Hosts the real API in memory against a real SQL Server database.
// xUnit creates one factory per test class, so every class gets its own database
// named ClaimDesk_Test_<guid>: created and migrated on start, dropped at the end.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DatabasePrefix = "ClaimDesk_Test_";

    // Used when ConnectionStrings__ClaimDesk is not set (it is set in CI).
    private const string LocalServer =
        "Server=localhost,14333;User Id=sa;Password=ClaimDesk_Dev#2026;TrustServerCertificate=True";

    // The same JSON conventions as the API: camelCase names and enums as strings.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _connectionString = BuildConnectionString();

    private static string BuildConnectionString()
    {
        var server = Environment.GetEnvironmentVariable("ConnectionStrings__ClaimDesk") ?? LocalServer;

        return new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = DatabasePrefix + Guid.NewGuid().ToString("N"),
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The API applies the migrations and seeds the users when it starts.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ClaimDesk"] = _connectionString,
            }));
    }

    public HttpClient ClientFor(int userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    public async Task InitializeAsync()
    {
        // Reading Services starts the host, which creates and migrates the database.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClaimDeskDbContext>();

        var database = db.Database.GetDbConnection().Database;
        if (!database.StartsWith(DatabasePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The tests must not run against the database '{database}'.");
        }

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClaimDeskDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}
