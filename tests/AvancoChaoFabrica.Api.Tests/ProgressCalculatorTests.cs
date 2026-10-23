using AvancoChaoFabrica.Api.Services;
using Xunit;

namespace AvancoChaoFabrica.Api.Tests;

public sealed class ProgressCalculatorTests
{
    [Fact]
    public void Calculate_WeightsProgressByItemWeightAndStageWeights()
    {
        var firstItem = Guid.NewGuid();
        var secondItem = Guid.NewGuid();
        StageProgressInput[] stages =
        [
            new(firstItem, 100m, 30m, 100m),
            new(firstItem, 100m, 70m, 0m),
            new(secondItem, 300m, 30m, 0m),
            new(secondItem, 300m, 70m, 100m)
        ];

        var result = ProgressCalculator.Calculate([firstItem, secondItem], stages);

        Assert.Equal(60m, result);
    }

    [Fact]
    public void Calculate_ReturnsNullWhenAnyStageHasNoReportedProgress()
    {
        var item = Guid.NewGuid();
        StageProgressInput[] stages =
        [
            new(item, 100m, 50m, 100m),
            new(item, 100m, 50m, null)
        ];

        var result = ProgressCalculator.Calculate([item], stages);

        Assert.Null(result);
    }

    [Fact]
    public void Calculate_ReturnsNullWhenStageWeightsDoNotTotalOneHundred()
    {
        var item = Guid.NewGuid();
        StageProgressInput[] stages =
        [
            new(item, 100m, 40m, 100m),
            new(item, 100m, 50m, 100m)
        ];

        var result = ProgressCalculator.Calculate([item], stages);

        Assert.Null(result);
    }

    [Fact]
    public void Calculate_ReturnsNullWhenAnItemWeightIsMissing()
    {
        var item = Guid.NewGuid();
        StageProgressInput[] stages =
        [
            new(item, null, 100m, 100m)
        ];

        var result = ProgressCalculator.Calculate([item], stages);

        Assert.Null(result);
    }
}
