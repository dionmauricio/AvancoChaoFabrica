using AvancoChaoFabrica.Api.Contracts;
using AvancoChaoFabrica.Api.Domain;
using AvancoChaoFabrica.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AvancoChaoFabrica.Api.Endpoints;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var suppliers = app.MapGroup("/api/suppliers").WithTags("Suppliers");
        suppliers.MapGet("/", async (bool? includeInactive, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Suppliers.AsNoTracking()
                .Where(x => includeInactive == true || x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new SupplierResponse(x.Id, x.Name, x.TaxId, x.IsActive))
                .ToListAsync(ct)));
        suppliers.MapPost("/", async (CreateSupplierRequest request, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Name)] = ["Supplier name is required."]
                });
            }

            var supplier = new Supplier { Name = request.Name.Trim(), TaxId = request.TaxId?.Trim() };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/suppliers/{supplier.Id}",
                new SupplierResponse(supplier.Id, supplier.Name, supplier.TaxId, supplier.IsActive));
        });
        suppliers.MapPut("/{id:guid}", async (
            Guid id,
            UpdateSupplierRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var supplier = await db.Suppliers.FindAsync([id], ct);
            if (supplier is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Name)] = ["Supplier name is required."]
                });
            }

            supplier.Name = request.Name.Trim();
            supplier.TaxId = request.TaxId?.Trim();
            supplier.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new SupplierResponse(supplier.Id, supplier.Name, supplier.TaxId, supplier.IsActive));
        });
        return app;
    }
}
