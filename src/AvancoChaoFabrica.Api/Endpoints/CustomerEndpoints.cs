using AvancoChaoFabrica.Api.Contracts;
using AvancoChaoFabrica.Api.Domain;
using AvancoChaoFabrica.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AvancoChaoFabrica.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/api/customers").WithTags("Customers");
        customers.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)));
        customers.MapPost("/", async (CreateCustomerRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Name)] = ["Customer name is required."]
                });
            }

            var customer = new Customer { Name = request.Name.Trim(), TaxId = request.TaxId?.Trim() };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/customers/{customer.Id}", customer);
        });
        customers.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCustomerRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var customer = await db.Customers.FindAsync([id], ct);
            if (customer is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Name)] = ["Customer name is required."]
                });
            }

            customer.Name = request.Name.Trim();
            customer.TaxId = request.TaxId?.Trim();
            customer.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(customer);
        });
        return app;
    }
}
