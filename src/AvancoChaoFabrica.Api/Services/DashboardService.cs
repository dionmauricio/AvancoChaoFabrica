using AvancoChaoFabrica.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AvancoChaoFabrica.Api.Services;

public sealed class DashboardService(AppDbContext db)
{
    public async Task<object> GetAsync(
        Guid? projectId,
        Guid? supplierId,
        Guid? productionOrderId,
        CancellationToken cancellationToken)
    {
        var items = await db.ProductionItems
            .AsNoTracking()
            .Where(x => x.IsActive && x.ProductionOrder!.IsActive)
            .Where(x => projectId == null || x.ProductionOrder!.ProjectId == projectId)
            .Where(x => supplierId == null || x.SupplierId == supplierId || x.ProductionOrder!.SupplierId == supplierId)
            .Where(x => productionOrderId == null || x.ProductionOrderId == productionOrderId)
            .Select(x => new
            {
                x.Id,
                x.ProductionOrderId,
                x.SupplierId,
                x.Tag,
                x.Description,
                x.TotalWeightKg,
                x.ContractualDate,
                x.RescheduledDate,
                DataDate = x.ProductionOrder!.DataDate
            })
            .ToListAsync(cancellationToken);

        var itemIds = items.Select(x => x.Id).ToArray();
        var progresses = await db.ItemStageProgress.AsNoTracking()
            .Where(x => itemIds.Contains(x.ProductionItemId))
            .GroupBy(x => new { x.ProductionItemId, x.StageCode })
            .Select(group => group.OrderByDescending(x => x.ReportDate).ThenByDescending(x => x.RecordedAt).First())
            .ToListAsync(cancellationToken);
        var orderIds = items.Select(x => x.ProductionOrderId).Distinct().ToArray();
        var weights = await db.OrderStageWeights.AsNoTracking()
            .Where(x => orderIds.Contains(x.ProductionOrderId))
            .ToListAsync(cancellationToken);
        var shipments = await db.ShipmentLines.AsNoTracking()
            .Where(x => itemIds.Contains(x.ProductionItemId))
            .GroupBy(x => x.ProductionItemId)
            .Select(group => new { ItemId = group.Key, WeightKg = group.Sum(x => x.WeightKg ?? 0m) })
            .ToDictionaryAsync(x => x.ItemId, x => x.WeightKg, cancellationToken);

        var progressByItem = progresses.GroupBy(x => x.ProductionItemId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(x => x.StageCode, x => x));
        var weightsByOrder = weights.GroupBy(x => x.ProductionOrderId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(x => x.StageCode, x => x));
        var datedItems = items.Where(x => x.RescheduledDate.HasValue || x.ContractualDate.HasValue).ToArray();
        var stageSummary = weights
            .GroupBy(x => new { x.StageCode, x.StageName, x.Sequence })
            .Select(group =>
            {
                var stageItems = items.Where(item =>
                    weightsByOrder.TryGetValue(item.ProductionOrderId, out var orderWeights) &&
                    orderWeights.ContainsKey(group.Key.StageCode)).ToArray();
                var reportedItems = stageItems.Where(item =>
                    progressByItem.TryGetValue(item.Id, out var itemProgress) &&
                    itemProgress.ContainsKey(group.Key.StageCode)).ToArray();
                var denominator = reportedItems.Sum(x => x.TotalWeightKg ?? 0m);
                var executed = reportedItems.Sum(item =>
                {
                    var amount = item.TotalWeightKg ?? 0m;
                    var percent = progressByItem[item.Id][group.Key.StageCode].CompletionPercent;
                    return amount * percent / 100m;
                });
                var weightDenominator = stageItems.Sum(x => x.TotalWeightKg ?? 0m);
                var reportedWeightPercent = stageItems.All(x => x.TotalWeightKg.HasValue) &&
                                            weightDenominator > 0m
                    ? stageItems.Sum(item =>
                    item.TotalWeightKg!.Value *
                    weightsByOrder[item.ProductionOrderId][group.Key.StageCode].WeightPercent) /
                    weightDenominator
                    : (decimal?)null;
                return new
                {
                    group.Key.StageCode,
                    group.Key.StageName,
                    group.Key.Sequence,
                    WeightPercent = reportedWeightPercent,
                    CompletionPercent = denominator == 0m ||
                                        reportedItems.Length != stageItems.Length ||
                                        stageItems.Any(x => !x.TotalWeightKg.HasValue)
                        ? (decimal?)null
                        : executed / denominator * 100m,
                    ReportedItems = reportedItems.Length,
                    ItemsWithoutReport = stageItems.Length - reportedItems.Length
                };
            })
            .OrderBy(x => x.Sequence)
            .ToArray();

        var progressInputs = new List<StageProgressInput>();
        foreach (var item in items)
        {
            if (!weightsByOrder.TryGetValue(item.ProductionOrderId, out var orderWeights))
            {
                continue;
            }

            foreach (var stageWeight in orderWeights.Values)
            {
                var completion = progressByItem.TryGetValue(item.Id, out var itemProgress) &&
                                 itemProgress.TryGetValue(stageWeight.StageCode, out var entry)
                    ? entry.CompletionPercent
                    : (decimal?)null;
                progressInputs.Add(new StageProgressInput(
                    item.Id, item.TotalWeightKg, stageWeight.WeightPercent, completion));
            }
        }

        var weightedProgress = ProgressCalculator.Calculate(
            items.Select(x => x.Id).ToArray(), progressInputs);
        var totalWeight = items.All(x => x.TotalWeightKg.HasValue)
            ? items.Sum(x => x.TotalWeightKg!.Value)
            : (decimal?)null;
        var shippedWeight = shipments.Values.Sum();
        var baseline = items.Where(x => x.DataDate.HasValue)
            .Select(x => x.DataDate)
            .Max() ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var overdueCount = datedItems.Count(x =>
            (x.RescheduledDate ?? x.ContractualDate) is { } due && due < baseline);

        return new
        {
            ItemCount = items.Count,
            TotalWeightKg = totalWeight,
            ShippedWeightKg = shippedWeight,
            ManufacturingBalanceKg = totalWeight is { } weight
                ? Math.Max(0m, weight - shippedWeight)
                : (decimal?)null,
            WeightedProgressPercent = weightedProgress,
            OrdersMissingStageConfiguration = items
                .Where(x => !weightsByOrder.ContainsKey(x.ProductionOrderId))
                .Select(x => x.ProductionOrderId)
                .Distinct()
                .Count(),
            ItemsMissingWeight = items.Count(x => !x.TotalWeightKg.HasValue),
            DataDate = baseline,
            OverdueItems = overdueCount,
            Stages = stageSummary
        };
    }
}
