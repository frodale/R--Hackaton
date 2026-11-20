using System.Globalization;

namespace RoAllocation.Core;

public interface IAllocationEngine
{
    AllocationResult Allocate(Scenario scenario, AllocationSettings settings);

    /// <summary>Runs every strategy with the same supply settings and marks the recommended one.</summary>
    IReadOnlyList<StrategyOutcome> Compare(Scenario scenario, AllocationSettings settings);
}

/// <summary>
/// Rule-based allocation of scarce materials between RØ Motion and RØ Marine orders.
/// Deterministic and explainable: the same input always gives the same answer.
/// </summary>
public sealed class AllocationEngine : IAllocationEngine
{
    /// <summary>Materials both business areas compete for; the Marine reserve applies to these.</summary>
    private static readonly Material[] SharedMaterials = [Material.CarbonFibre, Material.Timber];

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

    public AllocationResult Allocate(Scenario scenario, AllocationSettings settings)
    {
        var products = scenario.Products.ToDictionary(p => p.Id);
        var supply = EffectiveSupply(scenario.Supply, settings);
        decimal Score(Order o) => RevenuePerSupplyShare(products[o.ProductId], supply);

        Dictionary<string, int> allocated;
        switch (settings.Strategy)
        {
            case AllocationStrategy.CustomerCriticalFirst:
                allocated = Run(scenario, settings, products, supply, proRata: false,
                    open => open.OrderBy(o => o.Tier).ThenByDescending(Score));
                break;

            case AllocationStrategy.ProRata:
                allocated = Run(scenario, settings, products, supply, proRata: true,
                    open => open.OrderBy(o => o.Tier).ThenByDescending(Score));
                break;

            default:
                // Max revenue: try several greedy orderings (one per material, a combined one, and the other
                // strategies) and keep whichever earns the most. Simple, and never worse than the alternatives.
                var candidates = new List<Dictionary<string, int>>();
                foreach (var material in supply.Keys)
                {
                    decimal PerUnitOf(Order o)
                    {
                        var p = products[o.ProductId];
                        var use = p.BillOfMaterials.GetValueOrDefault(material);
                        return use > 0 ? p.UnitPrice / use : decimal.MaxValue;
                    }
                    candidates.Add(Run(scenario, settings, products, supply, proRata: false,
                        open => open.OrderByDescending(PerUnitOf).ThenByDescending(o => products[o.ProductId].UnitPrice).ThenBy(o => o.Tier)));
                }
                candidates.Add(Run(scenario, settings, products, supply, proRata: false,
                    open => open.OrderByDescending(Score).ThenBy(o => o.Tier)));
                candidates.Add(Run(scenario, settings, products, supply, proRata: false,
                    open => open.OrderBy(o => o.Tier).ThenByDescending(Score)));
                candidates.Add(Run(scenario, settings, products, supply, proRata: true,
                    open => open.OrderBy(o => o.Tier).ThenByDescending(Score)));

                decimal Revenue(Dictionary<string, int> a) =>
                    scenario.Orders.Sum(o => a[o.Id] * products[o.ProductId].UnitPrice);
                allocated = candidates.MaxBy(Revenue)!;
                break;
        }

        var orderResults = scenario.Orders
            .Select(o => new OrderAllocation(o, products[o.ProductId], allocated[o.Id]))
            .ToList();
        var balances = BuildBalances(scenario, supply, orderResults);
        var insights = BuildInsights(scenario, settings, orderResults, balances);

        return new AllocationResult(settings, orderResults, balances, insights);
    }

    /// <summary>One complete allocation: Marine reserve first (if any), then everything competes for the rest.</summary>
    private static Dictionary<string, int> Run(
        Scenario scenario,
        AllocationSettings settings,
        IReadOnlyDictionary<string, Product> products,
        IReadOnlyDictionary<Material, decimal> supply,
        bool proRata,
        Func<IEnumerable<Order>, IEnumerable<Order>> ordering)
    {
        var allocated = scenario.Orders.ToDictionary(o => o.Id, _ => 0);

        // Split shared materials into a Marine reserve and a common pool.
        var common = new Dictionary<Material, decimal>(supply);
        var marinePool = new Dictionary<Material, decimal>();
        foreach (var (material, amount) in supply)
        {
            if (SharedMaterials.Contains(material))
            {
                var reserved = Math.Floor(amount * settings.MarineReservePercent / 100m);
                marinePool[material] = reserved;
                common[material] = amount - reserved;
            }
            else
            {
                marinePool[material] = common[material];
            }
        }

        // Pass 1: Marine orders draw on their reserve first.
        if (settings.MarineReservePercent > 0)
        {
            var marineOrders = scenario.Orders.Where(o => products[o.ProductId].Area == BusinessArea.Marine);
            AllocatePass(marineOrders, marinePool, proRata, ordering, products, allocated);

            // Unused reserve goes back to the common pool.
            foreach (var (material, left) in marinePool)
                common[material] = SharedMaterials.Contains(material) ? common[material] + left : left;
        }

        // Pass 2: everything that is still open competes for the common pool.
        AllocatePass(scenario.Orders, common, proRata, ordering, products, allocated);
        return allocated;
    }

    public IReadOnlyList<StrategyOutcome> Compare(Scenario scenario, AllocationSettings settings)
    {
        var results = Enum.GetValues<AllocationStrategy>()
            .Select(s => Allocate(scenario, settings with { Strategy = s }))
            .ToList();

        // Recommended: no uncovered safety-critical orders first, then fewest elite gaps, then highest revenue.
        var best = results
            .OrderBy(r => r.GapsInTier(CustomerTier.SafetyCritical))
            .ThenBy(r => r.GapsInTier(CustomerTier.EliteDeadline))
            .ThenByDescending(r => r.Revenue)
            .First();

        return results.Select(r => new StrategyOutcome(r.Settings.Strategy, r, ReferenceEquals(r, best))).ToList();
    }

    private static Dictionary<Material, decimal> EffectiveSupply(
        IReadOnlyDictionary<Material, decimal> supply, AllocationSettings settings)
    {
        var result = new Dictionary<Material, decimal>(supply);
        if (result.TryGetValue(Material.CarbonFibre, out var carbon))
            result[Material.CarbonFibre] = Math.Floor(carbon * (100 - settings.CarbonFibreCutPercent) / 100m);
        return result;
    }

    private static void AllocatePass(
        IEnumerable<Order> orders,
        Dictionary<Material, decimal> pool,
        bool proRata,
        Func<IEnumerable<Order>, IEnumerable<Order>> ordering,
        IReadOnlyDictionary<string, Product> products,
        Dictionary<string, int> allocated)
    {
        var open = orders.Where(o => allocated[o.Id] < o.Quantity).ToList();
        if (open.Count == 0) return;

        if (proRata)
        {
            // Everyone gets the same share of what they asked for, limited by the scarcest material.
            var demand = new Dictionary<Material, decimal>();
            foreach (var order in open)
                foreach (var (material, perUnit) in products[order.ProductId].BillOfMaterials)
                    demand[material] = demand.GetValueOrDefault(material) + perUnit * (order.Quantity - allocated[order.Id]);

            var ratio = demand.Where(d => d.Value > 0)
                .Select(d => pool.GetValueOrDefault(d.Key) / d.Value)
                .DefaultIfEmpty(1m)
                .Min();
            ratio = Math.Min(1m, ratio);

            foreach (var order in open)
                Take(order, (int)Math.Floor((order.Quantity - allocated[order.Id]) * ratio), products, pool, allocated);
        }

        foreach (var order in ordering(open).ToList())
            Take(order, order.Quantity - allocated[order.Id], products, pool, allocated);
    }

    private static void Take(
        Order order, int wanted, IReadOnlyDictionary<string, Product> products,
        Dictionary<Material, decimal> pool, Dictionary<string, int> allocated)
    {
        if (wanted <= 0) return;
        var bom = products[order.ProductId].BillOfMaterials;

        var units = wanted;
        foreach (var (material, perUnit) in bom)
        {
            if (perUnit <= 0) continue;
            units = Math.Min(units, (int)Math.Floor(pool.GetValueOrDefault(material) / perUnit));
        }
        if (units <= 0) return;

        foreach (var (material, perUnit) in bom)
            pool[material] = pool.GetValueOrDefault(material) - perUnit * units;
        allocated[order.Id] += units;
    }

    /// <summary>Revenue per unit divided by the share of available supply one unit consumes.</summary>
    public static decimal RevenuePerSupplyShare(Product product, IReadOnlyDictionary<Material, decimal> supply)
    {
        decimal share = 0;
        foreach (var (material, perUnit) in product.BillOfMaterials)
        {
            var available = supply.GetValueOrDefault(material);
            if (perUnit <= 0) continue;
            if (available <= 0) return 0;
            share += perUnit / available;
        }
        return share == 0 ? product.UnitPrice : product.UnitPrice / share;
    }

    private static List<MaterialBalance> BuildBalances(
        Scenario scenario, IReadOnlyDictionary<Material, decimal> supply, IReadOnlyList<OrderAllocation> orders)
    {
        decimal Sum(Material m, BusinessArea area, Func<OrderAllocation, int> qty) =>
            orders.Where(o => o.Product.Area == area)
                  .Sum(o => o.Product.BillOfMaterials.GetValueOrDefault(m) * qty(o));

        return scenario.Materials.Select(info => new MaterialBalance(
            info,
            scenario.Supply.GetValueOrDefault(info.Material),
            supply.GetValueOrDefault(info.Material),
            Sum(info.Material, BusinessArea.Motion, o => o.Order.Quantity),
            Sum(info.Material, BusinessArea.Marine, o => o.Order.Quantity),
            Sum(info.Material, BusinessArea.Motion, o => o.AllocatedQuantity),
            Sum(info.Material, BusinessArea.Marine, o => o.AllocatedQuantity))).ToList();
    }

    private static List<Insight> BuildInsights(
        Scenario scenario, AllocationSettings settings,
        IReadOnlyList<OrderAllocation> orders, IReadOnlyList<MaterialBalance> balances)
    {
        var insights = new List<Insight>();
        string N(decimal v) => v.ToString("#,0.#", En);
        string Nok(decimal v) => $"NOK {(v / 1_000_000m).ToString("0.0", En)} M";

        var binding = balances.Where(b => b.Demand > 0).OrderBy(b => b.Coverage).FirstOrDefault();
        if (binding is not null && binding.Coverage < 1)
        {
            insights.Add(new Insight(InsightSeverity.Warning,
                $"{binding.Info.Name} is the bottleneck: {N(binding.Supply)} {binding.Info.Unit} available against " +
                $"{N(binding.Demand)} {binding.Info.Unit} ordered ({binding.Coverage.ToString("P0", En)} covered)."));
        }

        if (settings.CarbonFibreCutPercent > 0)
        {
            insights.Add(new Insight(InsightSeverity.Warning,
                $"Carbon fibre supply is cut by {settings.CarbonFibreCutPercent} %. The main supplier cannot confirm volumes (E-02)."));
        }

        foreach (var o in orders.Where(o => o.Order.Tier == CustomerTier.SafetyCritical && o.Status != FulfilmentStatus.Full))
        {
            insights.Add(new Insight(InsightSeverity.Critical,
                $"{o.Order.Customer} gets {o.AllocatedQuantity} of {o.Order.Quantity} {o.Product.Name}. " +
                "This is safety equipment. A shortfall needs explicit approval (B-04-02)."));
        }

        foreach (var o in orders.Where(o => o.Order.Tier == CustomerTier.EliteDeadline && o.Status != FulfilmentStatus.Full))
        {
            insights.Add(new Insight(InsightSeverity.Warning,
                $"{o.Order.Customer} gets {o.AllocatedQuantity} of {o.Order.Quantity} {o.Product.Name} before an event deadline."));
        }

        foreach (var o in orders.Where(o => o.Order.Tier == CustomerTier.StrategicDistributor && o.Status != FulfilmentStatus.Full))
        {
            insights.Add(new Insight(InsightSeverity.Warning,
                $"{o.Order.Customer} gets {o.AllocatedQuantity} of {o.Order.Quantity} {o.Product.Name}. " +
                "Send a confirmed partial date now instead of promising the full quantity (E-01)."));
        }

        if (settings.MarineReservePercent > 0)
        {
            var carbon = balances.FirstOrDefault(b => b.Info.Material == Material.CarbonFibre);
            if (carbon is not null)
            {
                var reserved = Math.Floor(carbon.Supply * settings.MarineReservePercent / 100m);
                insights.Add(new Insight(InsightSeverity.Info,
                    $"The Marine reserve of {settings.MarineReservePercent} % keeps {N(reserved)} kg carbon fibre for oars and boats, " +
                    "protecting the craft business and the brand."));
            }
        }

        var total = orders.Sum(o => o.RequestedRevenue);
        var delivered = orders.Sum(o => o.Revenue);
        var motion = orders.Where(o => o.Product.Area == BusinessArea.Motion).Sum(o => o.Revenue);
        var marine = orders.Where(o => o.Product.Area == BusinessArea.Marine).Sum(o => o.Revenue);
        insights.Add(new Insight(InsightSeverity.Info,
            $"Revenue that can be delivered: {Nok(delivered)} of {Nok(total)} ({(total == 0 ? 0 : delivered / total).ToString("P0", En)}). " +
            $"Motion {Nok(motion)}, Marine {Nok(marine)}."));

        return insights;
    }
}
