using AvancoChaoFabrica.Api.Contracts;
using AvancoChaoFabrica.Api.Domain;
using AvancoChaoFabrica.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AvancoChaoFabrica.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Production orders");
        orders.MapGet("/", async (
            Guid? projectId,
            Guid? supplierId,
            AppDbContext db,
            CancellationToken ct) =>
            Results.Ok(await db.ProductionOrders.AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x => projectId == null || x.ProjectId == projectId)
                .Where(x => supplierId == null || x.SupplierId == supplierId)
                .OrderBy(x => x.Number)
                .Select(x => new OrderResponse(
                    x.Id, x.Number, x.Kind, x.ProjectId, x.SupplierId, x.Revision, x.DataDate, x.IsActive))
                .ToListAsync(ct)));
        orders.MapPost("/", async (
            CreateOrderRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Number) ||
                !Enum.IsDefined(request.Kind) ||
                !await db.Projects.AnyAsync(x => x.Id == request.ProjectId && x.IsActive, ct) ||
                (request.SupplierId is { } supplierId &&
                 !await db.Suppliers.AnyAsync(x => x.Id == supplierId && x.IsActive, ct)))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["order"] = ["Provide an order number, valid type, active project, and valid supplier if supplied."]
                });
            }

            var order = new ProductionOrder
            {
                Number = request.Number.Trim(),
                Kind = request.Kind,
                ProjectId = request.ProjectId,
                SupplierId = request.SupplierId,
                Revision = request.Revision?.Trim(),
                DataDate = request.DataDate
            };
            db.ProductionOrders.Add(order);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{order.Id}", ToResponse(order));
        });
        orders.MapPut("/{id:guid}", async (
            Guid id,
            UpdateOrderRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var order = await db.ProductionOrders.FindAsync([id], ct);
            if (order is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Number) ||
                !Enum.IsDefined(request.Kind) ||
                !await db.Projects.AnyAsync(x => x.Id == request.ProjectId, ct) ||
                (request.SupplierId is { } supplierId &&
                 !await db.Suppliers.AnyAsync(x => x.Id == supplierId, ct)))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["order"] = ["Provide a number, valid type, existing project, and existing supplier if supplied."]
                });
            }

            order.Number = request.Number.Trim();
            order.Kind = request.Kind;
            order.ProjectId = request.ProjectId;
            order.SupplierId = request.SupplierId;
            order.Revision = request.Revision?.Trim();
            order.DataDate = request.DataDate;
            order.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToResponse(order));
        });
        orders.MapDelete("/{id:guid}", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var order = await db.ProductionOrders.FindAsync([id], ct);
            if (order is null)
            {
                return Results.NotFound();
            }

            order.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        orders.MapPost("/{id:guid}/items", async (
            Guid id,
            CreateItemRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var tag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim();
            var referenceTag = string.IsNullOrWhiteSpace(request.ReferenceTag)
                ? null
                : request.ReferenceTag.Trim();
            var drawing = string.IsNullOrWhiteSpace(request.Drawing) ? null : request.Drawing.Trim();
            var mark = string.IsNullOrWhiteSpace(request.Mark) ? null : request.Mark.Trim();
            if (request.ProductionOrderId != id ||
                request.Quantity < 0 ||
                request.UnitWeightKg < 0 ||
                request.TotalWeightKg < 0 ||
                (tag is null && referenceTag is null && drawing is null && mark is null) ||
                !await db.ProductionOrders.AnyAsync(x => x.Id == id && x.IsActive, ct))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["item"] = ["The order must be active; provide at least one item identifier and non-negative quantities and weights."]
                });
            }

            if (request.SupplierId is { } supplierId &&
                !await db.Suppliers.AnyAsync(x => x.Id == supplierId && x.IsActive, ct))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.SupplierId)] = ["Supplier does not exist or is inactive."]
                });
            }

            var item = new ProductionItem
            {
                ProductionOrderId = id,
                SupplierId = request.SupplierId,
                Drawing = drawing,
                Package = request.Package?.Trim(),
                Tag = tag,
                ReferenceTag = referenceTag,
                Mark = mark,
                Description = request.Description?.Trim(),
                Quantity = request.Quantity,
                Unit = request.Unit?.Trim(),
                UnitWeightKg = request.UnitWeightKg,
                TotalWeightKg = request.TotalWeightKg ?? (request.UnitWeightKg is { } unitWeight
                    ? request.Quantity * unitWeight
                    : null),
                ContractualDate = request.ContractualDate,
                RescheduledDate = request.RescheduledDate
            };
            db.ProductionItems.Add(item);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/items/{item.Id}", item);
        });
        orders.MapPut("/{id:guid}/stage-weights", async (
            Guid id,
            ReplaceStageWeightsRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (request.Stages is null ||
                request.Stages.Count == 0 ||
                request.Stages.Any(x => string.IsNullOrWhiteSpace(x.StageCode) ||
                                        string.IsNullOrWhiteSpace(x.StageName) ||
                                        x.WeightPercent < 0 || x.WeightPercent > 100) ||
                request.Stages.Select(x => x.StageCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                request.Stages.Count ||
                request.Stages.Sum(x => x.WeightPercent) != 100m)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["stages"] = ["Stages must have unique codes, valid weights, and weights totaling exactly 100%."]
                });
            }

            var order = await db.ProductionOrders
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (order is null)
            {
                return Results.NotFound();
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.OrderStageWeights.Where(x => x.ProductionOrderId == id).ExecuteDeleteAsync(ct);
            var stageWeights = request.Stages.Select(x => new OrderStageWeight
            {
                ProductionOrderId = id,
                StageCode = x.StageCode.Trim().ToUpperInvariant(),
                StageName = x.StageName.Trim(),
                Sequence = x.Sequence,
                WeightPercent = x.WeightPercent
            }).ToList();
            db.OrderStageWeights.AddRange(stageWeights);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(stageWeights.OrderBy(x => x.Sequence));
        });
        orders.MapPost("/{id:guid}/items/{itemId:guid}/progress", async (
            Guid id,
            Guid itemId,
            RecordProgressRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (request.CompletionPercent is < 0 or > 100 ||
                request.CompletedWeightKg < 0 ||
                string.IsNullOrWhiteSpace(request.StageCode))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["progress"] = ["Stage is required; completion must be between 0 and 100 and weight cannot be negative."]
                });
            }

            var item = await db.ProductionItems.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == itemId && x.ProductionOrderId == id && x.IsActive, ct);
            if (item is null)
            {
                return Results.NotFound();
            }

            if (request.CompletedWeightKg is { } completedWeight &&
                item.TotalWeightKg is { } itemWeight &&
                completedWeight > itemWeight)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.CompletedWeightKg)] = ["Completed weight cannot exceed the item's total weight."]
                });
            }

            var configured = await db.OrderStageWeights.AnyAsync(
                x => x.ProductionOrderId == id &&
                     x.StageCode == request.StageCode.Trim().ToUpperInvariant(), ct);
            if (!configured)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.StageCode)] = ["Stage is not configured for this order."]
                });
            }

            var progress = new ItemStageProgress
            {
                ProductionItemId = itemId,
                StageCode = request.StageCode.Trim().ToUpperInvariant(),
                CompletionPercent = request.CompletionPercent,
                CompletedWeightKg = request.CompletedWeightKg,
                ReportDate = request.ReportDate,
                Notes = request.Notes?.Trim()
            };
            db.ItemStageProgress.Add(progress);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{id}/items/{itemId}/progress/{progress.Id}", progress);
        });
        orders.MapGet("/{id:guid}/labor", async (
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (!await db.ProductionOrders.AnyAsync(x => x.Id == id, ct))
            {
                return Results.NotFound();
            }

            return Results.Ok(await db.LaborAllocations.AsNoTracking()
                .Where(x => x.ProductionOrderId == id)
                .OrderByDescending(x => x.WorkDate)
                .ToListAsync(ct));
        });
        orders.MapPost("/{id:guid}/labor", async (
            Guid id,
            CreateLaborAllocationRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.PersonOrTeam) ||
                request.Hours <= 0 ||
                !await db.ProductionOrders.AnyAsync(x => x.Id == id && x.IsActive, ct))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["labor"] = ["An active order, person or team, and positive hours are required."]
                });
            }

            var allocation = new LaborAllocation
            {
                ProductionOrderId = id,
                PersonOrTeam = request.PersonOrTeam.Trim(),
                Role = request.Role?.Trim(),
                WorkDate = request.WorkDate,
                Hours = request.Hours,
                Notes = request.Notes?.Trim()
            };
            db.LaborAllocations.Add(allocation);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/orders/{id}/labor/{allocation.Id}", allocation);
        });

        app.MapGet("/api/items", async (
            Guid? orderId,
            Guid? supplierId,
            string? tag,
            AppDbContext db,
            CancellationToken ct) =>
            Results.Ok(await db.ProductionItems.AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x => x.ProductionOrder!.IsActive)
                .Where(x => orderId == null || x.ProductionOrderId == orderId)
                .Where(x => supplierId == null || x.SupplierId == supplierId)
                .Where(x => tag == null || x.Tag == tag)
                .OrderBy(x => x.Tag)
                .ToListAsync(ct)));
        app.MapPut("/api/items/{itemId:guid}", async (
            Guid itemId,
            CreateItemRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var item = await db.ProductionItems.FindAsync([itemId], ct);
            if (item is null)
            {
                return Results.NotFound();
            }

            var tag = string.IsNullOrWhiteSpace(request.Tag) ? null : request.Tag.Trim();
            var referenceTag = string.IsNullOrWhiteSpace(request.ReferenceTag)
                ? null
                : request.ReferenceTag.Trim();
            var drawing = string.IsNullOrWhiteSpace(request.Drawing) ? null : request.Drawing.Trim();
            var mark = string.IsNullOrWhiteSpace(request.Mark) ? null : request.Mark.Trim();
            if ((tag is null && referenceTag is null && drawing is null && mark is null) ||
                request.Quantity < 0 ||
                request.UnitWeightKg < 0 ||
                request.TotalWeightKg < 0 ||
                !await db.ProductionOrders.AnyAsync(x => x.Id == request.ProductionOrderId, ct) ||
                (request.SupplierId is { } supplierId &&
                 !await db.Suppliers.AnyAsync(x => x.Id == supplierId, ct)))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["item"] = ["Provide a valid order and supplier, at least one item identifier, and non-negative values."]
                });
            }

            UpdateItem(item, request, tag, referenceTag, drawing, mark);
            await db.SaveChangesAsync(ct);
            return Results.Ok(item);
        });
        app.MapDelete("/api/items/{itemId:guid}", async (
            Guid itemId,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var item = await db.ProductionItems.FindAsync([itemId], ct);
            if (item is null)
            {
                return Results.NotFound();
            }

            item.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
        app.MapGet("/api/items/{itemId:guid}/shipments", async (
            Guid itemId,
            AppDbContext db,
            CancellationToken ct) =>
            Results.Ok(await db.ShipmentLines.AsNoTracking()
                .Where(x => x.ProductionItemId == itemId)
                .OrderByDescending(x => x.ShipmentDate)
                .ToListAsync(ct)));
        app.MapPost("/api/items/{itemId:guid}/shipments", async (
            Guid itemId,
            CreateShipmentLineRequest request,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (request.Quantity < 0 || request.WeightKg < 0 ||
                !await db.ProductionItems.AnyAsync(x => x.Id == itemId && x.IsActive, ct))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["shipment"] = ["An active item and non-negative shipment quantity and weight are required."]
                });
            }

            var shipment = new ShipmentLine
            {
                ProductionItemId = itemId,
                Quantity = request.Quantity,
                WeightKg = request.WeightKg,
                DocumentNumber = request.DocumentNumber?.Trim(),
                DocumentRevision = request.DocumentRevision?.Trim(),
                ManifestNumber = request.ManifestNumber?.Trim(),
                ShipmentDate = request.ShipmentDate
            };
            db.ShipmentLines.Add(shipment);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/items/{itemId}/shipments/{shipment.Id}", shipment);
        });

        return app;
    }

    private static void UpdateItem(
        ProductionItem item,
        CreateItemRequest request,
        string? tag,
        string? referenceTag,
        string? drawing,
        string? mark)
    {
        item.ProductionOrderId = request.ProductionOrderId;
        item.SupplierId = request.SupplierId;
        item.Drawing = drawing;
        item.Package = request.Package?.Trim();
        item.Tag = tag;
        item.ReferenceTag = referenceTag;
        item.Mark = mark;
        item.Description = request.Description?.Trim();
        item.Quantity = request.Quantity;
        item.Unit = request.Unit?.Trim();
        item.UnitWeightKg = request.UnitWeightKg;
        item.TotalWeightKg = request.TotalWeightKg ?? (request.UnitWeightKg is { } unitWeight
            ? request.Quantity * unitWeight
            : null);
        item.ContractualDate = request.ContractualDate;
        item.RescheduledDate = request.RescheduledDate;
    }

    private static OrderResponse ToResponse(ProductionOrder order) =>
        new(order.Id, order.Number, order.Kind, order.ProjectId, order.SupplierId,
            order.Revision, order.DataDate, order.IsActive);
}
