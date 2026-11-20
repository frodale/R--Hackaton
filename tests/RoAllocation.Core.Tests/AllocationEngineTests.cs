using RoAllocation.Core;

namespace RoAllocation.Core.Tests;

public class AllocationEngineTests
{
    private readonly Scenario _scenario = new InMemoryScenarioRepository().GetCurrent();
    private readonly AllocationEngine _engine = new();

    public static TheoryData<AllocationStrategy, int, int> AllSettings()
    {
        var data = new TheoryData<AllocationStrategy, int, int>();
        foreach (var strategy in Enum.GetValues<AllocationStrategy>())
            foreach (var cut in new[] { 0, 30, 60 })
                foreach (var reserve in new[] { 0, 25, 50 })
                    data.Add(strategy, cut, reserve);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Never_allocates_more_material_than_available(AllocationStrategy strategy, int cut, int reserve)
    {
        var result = _engine.Allocate(_scenario, new AllocationSettings(strategy, cut, reserve));

        Assert.All(result.Materials, m => Assert.True(m.Allocated <= m.Supply, $"{m.Info.Name}: {m.Allocated} > {m.Supply}"));
        Assert.All(result.Orders, o => Assert.InRange(o.AllocatedQuantity, 0, o.Order.Quantity));
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Max_revenue_is_never_beaten_by_another_strategy(AllocationStrategy strategy, int cut, int reserve)
    {
        var max = _engine.Allocate(_scenario, new AllocationSettings(AllocationStrategy.MaxRevenue, cut, reserve));
        var other = _engine.Allocate(_scenario, new AllocationSettings(strategy, cut, reserve));

        Assert.True(max.Revenue >= other.Revenue, $"{strategy}: {other.Revenue} > max {max.Revenue}");
    }

    [Fact]
    public void Customer_critical_first_serves_safety_critical_orders_in_full()
    {
        var result = _engine.Allocate(_scenario, new AllocationSettings(AllocationStrategy.CustomerCriticalFirst));

        Assert.Equal(0, result.GapsInTier(CustomerTier.SafetyCritical));
    }

    [Fact]
    public void Max_revenue_leaves_rescue_oars_short_and_says_so()
    {
        var result = _engine.Allocate(_scenario, new AllocationSettings(AllocationStrategy.MaxRevenue));

        Assert.True(result.GapsInTier(CustomerTier.SafetyCritical) > 0);
        Assert.Contains(result.Insights, i => i.Severity == InsightSeverity.Critical);
    }

    [Fact]
    public void Supply_cut_reduces_carbon_fibre_and_delivered_revenue()
    {
        var normal = _engine.Allocate(_scenario, new AllocationSettings(AllocationStrategy.CustomerCriticalFirst));
        var cut = _engine.Allocate(_scenario, new AllocationSettings(AllocationStrategy.CustomerCriticalFirst, CarbonFibreCutPercent: 30));

        Assert.Equal(2_100m, cut.Materials.Single(m => m.Info.Material == Material.CarbonFibre).Supply);
        Assert.True(cut.Revenue < normal.Revenue);
    }

    [Fact]
    public void Marine_reserve_protects_marine_carbon_fibre()
    {
        var without = _engine.Allocate(_scenario, new AllocationSettings(AllocationStrategy.MaxRevenue, 30, 0));
        var with = _engine.Allocate(_scenario, new AllocationSettings(AllocationStrategy.MaxRevenue, 30, 40));

        decimal MarineCarbon(AllocationResult r) => r.Materials.Single(m => m.Info.Material == Material.CarbonFibre).AllocatedMarine;
        Assert.True(MarineCarbon(with) > MarineCarbon(without));
    }

    [Fact]
    public void Compare_recommends_exactly_one_strategy_without_safety_gaps()
    {
        var outcomes = _engine.Compare(_scenario, new AllocationSettings(AllocationStrategy.MaxRevenue));

        var recommended = Assert.Single(outcomes, o => o.Recommended);
        Assert.Equal(0, recommended.Result.GapsInTier(CustomerTier.SafetyCritical));
    }
}
