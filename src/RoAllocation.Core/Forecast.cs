using System.Globalization;

namespace RoAllocation.Core;

/// <summary>Monthly demand multiplier per order line (1.0 = the baseline order quantity).</summary>
public sealed record DemandProfile(string OrderId, IReadOnlyList<decimal> Factors);

/// <summary>Something that changes supply or demand in a given month, shown on the timeline.</summary>
public sealed record PlanEvent(int MonthIndex, string Text);

public sealed record ForecastPlan(
    DateOnly StartMonth,
    int Months,
    IReadOnlyList<DemandProfile> Demand,
    IReadOnlyDictionary<Material, IReadOnlyList<decimal>> Supply,
    IReadOnlyDictionary<CustomerTier, decimal> CancellationRate,
    IReadOnlyList<PlanEvent> Events);

public sealed record MonthForecast(
    int Index,
    DateOnly Month,
    Scenario Scenario,
    AllocationResult Result,
    decimal NewDemandRevenue,
    decimal BacklogInRevenue,
    decimal CarriedRevenue,
    decimal CancelledRevenue)
{
    public string Label => Month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    public string ShortLabel => Month.ToString("MMM", CultureInfo.InvariantCulture);
}

public sealed record Forecast(AllocationSettings Settings, IReadOnlyList<MonthForecast> Months, IReadOnlyList<PlanEvent> Events)
{
    public decimal Revenue => Months.Sum(m => m.Result.Revenue);
    public decimal NewDemand => Months.Sum(m => m.NewDemandRevenue);
    public decimal Cancelled => Months.Sum(m => m.CancelledRevenue);
    public decimal EndBacklog => Months.Count == 0 ? 0 : Months[^1].CarriedRevenue;
    public decimal PeakBacklog => Months.Count == 0 ? 0 : Months.Max(m => m.CarriedRevenue);
    public int MonthsWithSafetyGaps => Months.Count(m => m.Result.GapsInTier(CustomerTier.SafetyCritical) > 0);
}

public interface IForecastEngine
{
    Forecast Run(Scenario baseline, ForecastPlan plan, AllocationSettings settings);
}

/// <summary>
/// Runs the allocation month by month. What can't be delivered becomes backlog for the next month;
/// part of the backlog is cancelled each month depending on how patient the customer tier is.
/// </summary>
public sealed class ForecastEngine(IAllocationEngine allocation) : IForecastEngine
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

    public Forecast Run(Scenario baseline, ForecastPlan plan, AllocationSettings settings)
    {
        var products = baseline.Products.ToDictionary(p => p.Id);
        var profiles = plan.Demand.ToDictionary(d => d.OrderId);
        var backlog = baseline.Orders.ToDictionary(o => o.Id, _ => 0);
        var months = new List<MonthForecast>();

        for (var i = 0; i < plan.Months; i++)
        {
            var month = plan.StartMonth.AddMonths(i);
            var newQty = new Dictionary<string, int>();
            var orders = new List<Order>();

            foreach (var o in baseline.Orders)
            {
                var factor = profiles.TryGetValue(o.Id, out var p) && i < p.Factors.Count ? p.Factors[i] : 1m;
                var fresh = (int)Math.Round(o.Quantity * factor, MidpointRounding.AwayFromZero);
                newQty[o.Id] = fresh;
                var waiting = backlog[o.Id];
                var note = waiting > 0 ? $"{o.Note} · incl. {waiting.ToString("#,0", En)} from backlog" : o.Note;
                orders.Add(o with { Quantity = fresh + waiting, Note = note });
            }

            var supply = baseline.Supply.ToDictionary(
                s => s.Key,
                s => plan.Supply.TryGetValue(s.Key, out var series) && i < series.Count ? series[i] : s.Value);

            var scenario = baseline with
            {
                Name = $"Operation FULL FART – {month.ToString("MMMM yyyy", En)}",
                Period = month.ToString("MMMM yyyy", En),
                Orders = orders,
                Supply = supply,
            };

            var result = allocation.Allocate(scenario, settings);

            decimal carried = 0, cancelled = 0, backlogIn = 0, newDemand = 0;
            foreach (var oa in result.Orders)
            {
                var price = oa.Product.UnitPrice;
                backlogIn += backlog[oa.Order.Id] * price;
                newDemand += newQty[oa.Order.Id] * price;

                var unfilled = oa.Order.Quantity - oa.AllocatedQuantity;
                var lost = (int)Math.Floor(unfilled * plan.CancellationRate.GetValueOrDefault(oa.Order.Tier));
                backlog[oa.Order.Id] = unfilled - lost;
                carried += (unfilled - lost) * price;
                cancelled += lost * price;
            }

            months.Add(new MonthForecast(i, month, scenario, result, newDemand, backlogIn, carried, cancelled));
        }

        return new Forecast(settings, months, plan.Events);
    }
}
