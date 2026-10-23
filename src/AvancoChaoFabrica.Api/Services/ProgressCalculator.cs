namespace AvancoChaoFabrica.Api.Services;

public sealed record StageProgressInput(
    Guid ItemId,
    decimal? ItemWeightKg,
    decimal StageWeightPercent,
    decimal? CompletionPercent);

public static class ProgressCalculator
{
    public static decimal? Calculate(
        IReadOnlyCollection<Guid> itemIds,
        IReadOnlyCollection<StageProgressInput> stages)
    {
        if (itemIds.Count == 0 || itemIds.Distinct().Count() != itemIds.Count)
        {
            return null;
        }

        var items = stages.GroupBy(x => x.ItemId).ToDictionary(x => x.Key, x => x.ToArray());
        if (items.Count != itemIds.Count || itemIds.Any(id => !items.ContainsKey(id)))
        {
            return null;
        }

        decimal totalWeightKg = 0m;
        decimal completedWeightedKg = 0m;
        foreach (var itemId in itemIds)
        {
            var itemStages = items[itemId];
            var weight = itemStages[0].ItemWeightKg;
            if (weight is null or <= 0m ||
                itemStages.Any(stage =>
                    stage.ItemWeightKg != weight ||
                    stage.StageWeightPercent is < 0m or > 100m ||
                    stage.CompletionPercent is null or < 0m or > 100m) ||
                itemStages.Sum(stage => stage.StageWeightPercent) != 100m)
            {
                return null;
            }

            totalWeightKg += weight.Value;
            completedWeightedKg += itemStages.Sum(stage =>
                weight.Value * stage.StageWeightPercent / 100m *
                stage.CompletionPercent!.Value / 100m);
        }

        return totalWeightKg == 0m
            ? null
            : completedWeightedKg / totalWeightKg * 100m;
    }
}
