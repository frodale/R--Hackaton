namespace RoAllocation.Core;

public enum BusinessArea { Motion, Marine }

public enum Material { CarbonFibre, Timber, Electronics }

/// <summary>Lower value = served first when the strategy respects customer priority.</summary>
public enum CustomerTier { SafetyCritical = 0, EliteDeadline = 1, StrategicDistributor = 2, Standard = 3 }

public enum AllocationStrategy { MaxRevenue, CustomerCriticalFirst, ProRata }

public enum FulfilmentStatus { Full, Partial, None }

public enum InsightSeverity { Info, Warning, Critical }

public sealed record MaterialInfo(Material Material, string Name, string Unit);

public sealed record Product(
    string Id,
    string Name,
    BusinessArea Area,
    decimal UnitPrice,
    IReadOnlyDictionary<Material, decimal> BillOfMaterials);

public sealed record Order(
    string Id,
    string Customer,
    string ProductId,
    int Quantity,
    CustomerTier Tier,
    string Note);

public sealed record Scenario(
    string Name,
    string Period,
    IReadOnlyList<MaterialInfo> Materials,
    IReadOnlyList<Product> Products,
    IReadOnlyList<Order> Orders,
    IReadOnlyDictionary<Material, decimal> Supply);

public sealed record AllocationSettings(
    AllocationStrategy Strategy,
    int CarbonFibreCutPercent = 0,
    int MarineReservePercent = 0);

public sealed record OrderAllocation(Order Order, Product Product, int AllocatedQuantity)
{
    public decimal RequestedRevenue => Order.Quantity * Product.UnitPrice;
    public decimal Revenue => AllocatedQuantity * Product.UnitPrice;
    public decimal RevenueAtRisk => RequestedRevenue - Revenue;
    public double FillRate => Order.Quantity == 0 ? 1 : (double)AllocatedQuantity / Order.Quantity;

    public FulfilmentStatus Status =>
        AllocatedQuantity >= Order.Quantity ? FulfilmentStatus.Full
        : AllocatedQuantity > 0 ? FulfilmentStatus.Partial
        : FulfilmentStatus.None;
}

public sealed record MaterialBalance(
    MaterialInfo Info,
    decimal OriginalSupply,
    decimal Supply,
    decimal DemandMotion,
    decimal DemandMarine,
    decimal AllocatedMotion,
    decimal AllocatedMarine)
{
    public decimal Demand => DemandMotion + DemandMarine;
    public decimal Allocated => AllocatedMotion + AllocatedMarine;
    public decimal Shortfall => Math.Max(0, Demand - Supply);
    public double Coverage => Demand == 0 ? 1 : Math.Min(1, (double)(Supply / Demand));
}

public sealed record Insight(InsightSeverity Severity, string Text);

public sealed record AllocationResult(
    AllocationSettings Settings,
    IReadOnlyList<OrderAllocation> Orders,
    IReadOnlyList<MaterialBalance> Materials,
    IReadOnlyList<Insight> Insights)
{
    public decimal OrderBook => Orders.Sum(o => o.RequestedRevenue);
    public decimal Revenue => Orders.Sum(o => o.Revenue);
    public decimal RevenueAtRisk => OrderBook - Revenue;
    public decimal RevenueFor(BusinessArea area) => Orders.Where(o => o.Product.Area == area).Sum(o => o.Revenue);
    public int FullyServed => Orders.Count(o => o.Status == FulfilmentStatus.Full);
    public int GapsInTier(CustomerTier tier) => Orders.Count(o => o.Order.Tier == tier && o.Status != FulfilmentStatus.Full);
}

public sealed record StrategyOutcome(AllocationStrategy Strategy, AllocationResult Result, bool Recommended);
