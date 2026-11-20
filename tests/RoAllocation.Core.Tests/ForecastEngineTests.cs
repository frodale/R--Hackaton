using RoAllocation.Core;

namespace RoAllocation.Core.Tests;

public class ForecastEngineTests
{
    private readonly InMemoryScenarioRepository _repository = new();
    private readonly ForecastEngine _engine = new(new AllocationEngine());

    private Forecast Run(AllocationStrategy strategy, int cut = 0, int reserve = 0) =>
        _engine.Run(_repository.GetCurrent(), _repository.GetForecastPlan(), new AllocationSettings(strategy, cut, reserve));

    [Fact]
    public void Covers_december_2026_to_december_2027()
    {
        var forecast = Run(AllocationStrategy.CustomerCriticalFirst);

        Assert.Equal(13, forecast.Months.Count);
        Assert.Equal(new DateOnly(2026, 12, 1), forecast.Months[0].Month);
        Assert.Equal(new DateOnly(2027, 12, 1), forecast.Months[^1].Month);
    }

    [Theory]
    [InlineData(AllocationStrategy.MaxRevenue)]
    [InlineData(AllocationStrategy.CustomerCriticalFirst)]
    [InlineData(AllocationStrategy.ProRata)]
    public void Unfilled_orders_are_either_carried_or_cancelled(AllocationStrategy strategy)
    {
        var forecast = Run(strategy, cut: 30);

        foreach (var m in forecast.Months)
            Assert.Equal(m.Result.RevenueAtRisk, m.CarriedRevenue + m.CancelledRevenue);

        for (var i = 1; i < forecast.Months.Count; i++)
            Assert.Equal(forecast.Months[i - 1].CarriedRevenue, forecast.Months[i].BacklogInRevenue);
    }

    [Fact]
    public void Safety_critical_backlog_is_never_cancelled()
    {
        var forecast = Run(AllocationStrategy.MaxRevenue, cut: 30);

        foreach (var m in forecast.Months.SkipLast(1))
        {
            var rescue = m.Result.Orders.Single(o => o.Order.Tier == CustomerTier.SafetyCritical);
            var next = forecast.Months[m.Index + 1].Scenario.Orders.Single(o => o.Id == rescue.Order.Id);
            var fresh = _repository.GetCurrent().Orders.Single(o => o.Id == rescue.Order.Id).Quantity; // steady demand
            Assert.Equal(rescue.Order.Quantity - rescue.AllocatedQuantity + fresh, next.Quantity);
        }
    }

    [Fact]
    public void Uses_the_monthly_supply_plan()
    {
        var forecast = Run(AllocationStrategy.CustomerCriticalFirst);

        Assert.Equal(3_000m, forecast.Months[0].Scenario.Supply[Material.CarbonFibre]);
        Assert.Equal(4_000m, forecast.Months[4].Scenario.Supply[Material.CarbonFibre]);
    }
}
