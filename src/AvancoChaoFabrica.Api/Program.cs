using AvancoChaoFabrica.Api.Endpoints;
using AvancoChaoFabrica.Api.Infrastructure;
using AvancoChaoFabrica.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

EnvironmentFile.Load();

var builder = WebApplication.CreateBuilder(args);
var authMode = builder.Configuration["Authentication:Mode"]?.Trim();
authMode = string.IsNullOrWhiteSpace(authMode) ? "Local" : authMode;
var identityAuthority = builder.Configuration["Authentication:Authority"];
var identityAudience = builder.Configuration["Authentication:Audience"];
var tokenIssuer = builder.Configuration["Authentication:Issuer"] ?? "AvancoChaoFabrica.Local";
var tokenAudience = identityAudience ?? "avanco-api";
var signingKey = builder.Configuration["Authentication:SigningKey"];
if (authMode.Equals("OpenIdConnect", StringComparison.OrdinalIgnoreCase) &&
    (string.IsNullOrWhiteSpace(identityAuthority) || string.IsNullOrWhiteSpace(identityAudience)))
{
    throw new InvalidOperationException(
        "OpenID Connect mode requires Authentication:Authority and Authentication:Audience.");
}

if (authMode.Equals("Local", StringComparison.OrdinalIgnoreCase) &&
    (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32))
{
    throw new InvalidOperationException(
        "Local authentication requires Authentication:SigningKey with at least 32 UTF-8 bytes.");
}

if (!authMode.Equals("Local", StringComparison.OrdinalIgnoreCase) &&
    !authMode.Equals("OpenIdConnect", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Authentication:Mode must be Local or OpenIdConnect.");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL is not configured. Add ConnectionStrings__DefaultConnection to the " +
        ".env file in the project root or set the matching environment variable, then restart the API.");
}

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
{
    if (authMode.Equals("OpenIdConnect", StringComparison.OrdinalIgnoreCase))
    {
        options.Authority = identityAuthority;
        options.Audience = identityAudience;
        options.RequireHttpsMetadata = true;
    }
    else
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = tokenIssuer,
            ValidateAudience = true,
            ValidAudience = tokenAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddRateLimiter(options => options.AddPolicy("local-login", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        })));
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.PasswordHasher<AvancoChaoFabrica.Api.Domain.LocalUser>>();
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = 25 * 1024 * 1024);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<WorkbookImportService>();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins);
    }

    policy.AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var isApiMutation = context.Request.Path.StartsWithSegments("/api") &&
                        !HttpMethods.IsGet(context.Request.Method) &&
                        !HttpMethods.IsHead(context.Request.Method) &&
                        !HttpMethods.IsOptions(context.Request.Method) &&
                        !context.Request.Path.StartsWithSegments("/api/auth/login");
    if (isApiMutation &&
        !context.User.IsInRole("Admin") &&
        !context.User.IsInRole("Operator"))
    {
        context.Response.StatusCode = context.User.Identity?.IsAuthenticated == true
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;
        return;
    }

    await next(context);
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
{
    try
    {
        var databaseAvailable = await db.Database.CanConnectAsync(ct);
        return databaseAvailable
            ? Results.Ok(new { status = "ok", database = "connected" })
            : Results.Json(new { status = "degraded", database = "unavailable" }, statusCode: 503);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        return Results.Json(new { status = "degraded", database = "unavailable" }, statusCode: 503);
    }
}).AllowAnonymous();
app.MapCustomerEndpoints();
app.MapProjectEndpoints();
app.MapSupplierEndpoints();
app.MapOrderEndpoints();
app.MapDashboardEndpoints();
app.MapImportEndpoints();
app.MapAuthEndpoints(authMode.Equals("Local", StringComparison.OrdinalIgnoreCase));

if (app.Environment.IsDevelopment() ||
    builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    if (authMode.Equals("Local", StringComparison.OrdinalIgnoreCase))
    {
        var adminUserName = builder.Configuration["BootstrapAdmin:UserName"]?.Trim();
        var adminPassword = builder.Configuration["BootstrapAdmin:Password"];
        if (await db.LocalUsers.CountAsync() == 0 &&
            (string.IsNullOrWhiteSpace(adminUserName) || string.IsNullOrEmpty(adminPassword) ||
             adminPassword.Length < 12))
        {
            throw new InvalidOperationException(
                "The local user database is empty. Configure BootstrapAdmin:UserName and a " +
                "BootstrapAdmin:Password of at least 12 characters for the first startup.");
        }

        if (!string.IsNullOrWhiteSpace(adminUserName) && !string.IsNullOrWhiteSpace(adminPassword) &&
            !await db.LocalUsers.AnyAsync(x => x.NormalizedUserName ==
                AuthEndpoints.NormalizeUserName(adminUserName)))
        {
            var admin = new AvancoChaoFabrica.Api.Domain.LocalUser
            {
                UserName = adminUserName,
                NormalizedUserName = AuthEndpoints.NormalizeUserName(adminUserName),
                PasswordHash = string.Empty,
                Role = "Admin"
            };
            var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.PasswordHasher<AvancoChaoFabrica.Api.Domain.LocalUser>>();
            admin.PasswordHash = hasher.HashPassword(admin, adminPassword);
            db.LocalUsers.Add(admin);
            await db.SaveChangesAsync();
        }
    }
}

app.Run();
