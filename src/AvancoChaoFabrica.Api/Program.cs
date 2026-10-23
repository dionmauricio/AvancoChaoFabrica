using AvancoChaoFabrica.Api.Endpoints;
using AvancoChaoFabrica.Api.Infrastructure;
using AvancoChaoFabrica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;

EnvironmentFile.Load();

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL is not configured. Add ConnectionStrings__DefaultConnection to the " +
        ".env file in the project root or set the matching environment variable, then restart the API.");
}

builder.Services.AddOpenApi();
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = 25 * 1024 * 1024);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<WorkbookImportService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevelopment", policy =>
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("AngularDevelopment");
}

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    var databaseAvailable = await db.Database.CanConnectAsync(ct);
    return Results.Ok(new
    {
        status = databaseAvailable ? "ok" : "degraded",
        database = databaseAvailable ? "connected" : "unavailable"
    });
});
app.MapCustomerEndpoints();
app.MapProjectEndpoints();
app.MapSupplierEndpoints();
app.MapOrderEndpoints();
app.MapDashboardEndpoints();
app.MapImportEndpoints();

app.Run();
