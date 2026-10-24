using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AvancoChaoFabrica.Api.Domain;
using AvancoChaoFabrica.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AvancoChaoFabrica.Api.Endpoints;

public sealed record LocalLoginRequest(string UserName, string Password);
public sealed record CreateLocalUserRequest(string UserName, string Password, string Role);

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder app,
        bool localAuthentication)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");
        if (localAuthentication)
        {
            auth.MapPost("/login", async (
            LocalLoginRequest request,
            AppDbContext db,
            PasswordHasher<LocalUser> passwordHasher,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
            {
                return Results.Unauthorized();
            }

            var normalizedUserName = NormalizeUserName(request.UserName);
            var user = await db.LocalUsers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName && x.IsActive, ct);
            if (user is null || passwordHasher.VerifyHashedPassword(
                    user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            var issuer = configuration["Authentication:Issuer"]!;
            var audience = configuration["Authentication:Audience"]!;
            var signingKey = configuration["Authentication:SigningKey"]!;
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role)
            };
            var token = new JwtSecurityToken(
                issuer,
                audience,
                claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    SecurityAlgorithms.HmacSha256));

            return Results.Ok(new
            {
                accessToken = new JwtSecurityTokenHandler().WriteToken(token),
                expiresAt = token.ValidTo,
                userName = user.UserName,
                role = user.Role
            });
            }).AllowAnonymous().RequireRateLimiting("local-login");
        }

        auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            userName = user.Identity?.Name,
            role = user.FindFirstValue(ClaimTypes.Role)
        }));

        if (localAuthentication)
        {
            auth.MapPost("/users", async (
            CreateLocalUserRequest request,
            AppDbContext db,
            PasswordHasher<LocalUser> passwordHasher,
            CancellationToken ct) =>
        {
            var userName = request.UserName?.Trim();
            var role = request.Role?.Trim();
            if (string.IsNullOrWhiteSpace(userName) || userName.Length > 100 ||
                string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12 ||
                role is not ("Admin" or "Operator" or "Reader"))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["user"] = ["Username is required; password must have at least 12 characters; role must be Admin, Operator, or Reader."]
                });
            }

            var normalized = NormalizeUserName(userName);
            if (await db.LocalUsers.AnyAsync(x => x.NormalizedUserName == normalized, ct))
            {
                return Results.Conflict(new { error = "Username is already in use." });
            }

            var user = new LocalUser
            {
                UserName = userName,
                NormalizedUserName = normalized,
                PasswordHash = string.Empty,
                Role = role
            };
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            db.LocalUsers.Add(user);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/auth/users/{user.Id}", new
            {
                user.Id,
                user.UserName,
                user.Role,
                user.CreatedAt
            });
            }).RequireAuthorization(policy => policy.RequireRole("Admin"));
        }

        return app;
    }

    public static string NormalizeUserName(string userName) => userName.Trim().ToUpperInvariant();
}
