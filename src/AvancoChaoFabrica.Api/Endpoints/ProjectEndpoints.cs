using AvancoChaoFabrica.Api.Contracts;
using AvancoChaoFabrica.Api.Domain;
using AvancoChaoFabrica.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AvancoChaoFabrica.Api.Endpoints;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("/api/projects").WithTags("Projects");
        projects.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Projects.AsNoTracking().OrderBy(x => x.Name)
                .Select(x => new ProjectResponse(x.Id, x.Name, x.Code, x.CustomerId, x.IsActive))
                .ToListAsync(ct)));
        projects.MapPost("/", async (CreateProjectRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) ||
                !await db.Customers.AnyAsync(x => x.Id == request.CustomerId && x.IsActive, ct))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["project"] = ["A project name and an active customer are required."]
                });
            }

            var project = new Project
            {
                Name = request.Name.Trim(),
                Code = request.Code?.Trim(),
                CustomerId = request.CustomerId
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/projects/{project.Id}",
                new ProjectResponse(project.Id, project.Name, project.Code, project.CustomerId, project.IsActive));
        });
        projects.MapPut("/{id:guid}", async (
            Guid id,
            UpdateProjectRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var project = await db.Projects.FindAsync([id], ct);
            if (project is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Name) ||
                !await db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["project"] = ["A project name and an existing customer are required."]
                });
            }

            project.Name = request.Name.Trim();
            project.Code = request.Code?.Trim();
            project.CustomerId = request.CustomerId;
            project.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ProjectResponse(project.Id, project.Name, project.Code,
                project.CustomerId, project.IsActive));
        });
        return app;
    }
}
