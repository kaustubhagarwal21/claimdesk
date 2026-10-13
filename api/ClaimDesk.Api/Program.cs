using System.Text.Json.Serialization;
using ClaimDesk.Api.Data;
using ClaimDesk.Api.Infrastructure;
using ClaimDesk.Api.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The connection string is read when the first DbContext is created, not here,
// so environment variables and the integration tests can override it.
builder.Services.AddDbContext<ClaimDeskDbContext>((services, options) =>
    options.UseSqlServer(services.GetRequiredService<IConfiguration>().GetConnectionString("ClaimDesk")));

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<PolicyService>();
builder.Services.AddScoped<ClaimService>();
builder.Services.AddScoped<ReportService>();

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<CurrentUserFilter>();

        // Report validation errors under the JSON field names (sumInsured, not SumInsured).
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();   // problem details for empty error responses such as an unknown route

// The Docker image has the built React app in wwwroot, so one container serves the UI and the API.
// In development wwwroot is empty and the Vite dev server serves the UI instead.
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// The app creates its own schema and seed rows when it starts, which keeps the demo to one command.
// A real deployment would run the migrations as a separate release step.
await DbSeeder.MigrateAndSeedAsync(app.Services);

app.MapControllers();

// An unknown /api path stays a 404. Any other path is a page of the React app
// (for example /claims/7), so the browser gets index.html and the router takes over.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
