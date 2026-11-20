namespace RoAllocation.Core;

public interface IScenarioRepository
{
    /// <summary>Baseline: one average month of orders and supply.</summary>
    Scenario GetCurrent();

    /// <summary>How demand and supply develop month by month from the baseline.</summary>
    ForecastPlan GetForecastPlan();
}

/// <summary>
/// Mock data inspired by the RØ case (E-01, E-02, R01, R02). Prices, bills of material, quantities and
/// supply are our own assumptions for the demo; replace this class with real storage later.
/// </summary>
public sealed class InMemoryScenarioRepository : IScenarioRepository
{
    private static readonly Scenario Current = Build();
    private static readonly ForecastPlan Plan = BuildPlan();

    public Scenario GetCurrent() => Current;

    public ForecastPlan GetForecastPlan() => Plan;

    /// <summary>
    /// December 2026 to December 2027. All patterns are assumptions:
    /// the rowing boom keeps growing into summer 2027 and then fades (C12), the leisure rowing season peaks
    /// in spring and summer, the national team orders before the championships, and alternative suppliers
    /// for carbon fibre (P03, A-28) and electronics come online during 2027.
    /// </summary>
    private static ForecastPlan BuildPlan()
    {
        const int months = 13; // Dec 2026 .. Dec 2027

        //                         Dec   Jan   Feb   Mar   Apr   May   Jun   Jul   Aug   Sep   Oct   Nov   Dec
        decimal[] boom =        [ 1.00m, 1.20m, 1.12m, 1.19m, 1.26m, 1.34m, 1.42m, 1.42m, 1.40m, 1.33m, 1.26m, 1.20m, 1.25m ];
        decimal[] leisure =     [ 0.50m, 0.50m, 0.70m, 1.00m, 1.30m, 1.50m, 1.40m, 1.20m, 1.00m, 0.80m, 0.60m, 0.50m, 0.50m ];
        decimal[] elite =       [ 0.40m, 0.50m, 0.80m, 1.20m, 1.50m, 1.50m, 1.00m, 0.60m, 0.50m, 0.40m, 0.40m, 0.40m, 0.40m ];
        decimal[] steady =      [ 1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m ];
        decimal[] ceremonial =  [ 0.50m, 0.30m, 0.50m, 1.00m, 1.50m, 2.50m, 1.00m, 0.50m, 0.50m, 0.50m, 0.50m, 0.50m, 0.80m ];

        DemandProfile[] demand =
        [
            new("M-101", boom), new("M-102", boom), new("M-103", boom), new("M-104", boom), new("M-105", boom),
            new("R-201", steady),
            new("R-202", elite),
            new("R-203", leisure), new("R-204", leisure), new("R-206", leisure),
            new("R-205", ceremonial),
        ];

        var supply = new Dictionary<Material, IReadOnlyList<decimal>>
        {
            //                         Dec    Jan    Feb    Mar    Apr    May    Jun    Jul    Aug    Sep    Oct    Nov    Dec
            [Material.CarbonFibre] = [ 3000m, 3000m, 3000m, 3000m, 4000m, 4000m, 4000m, 4000m, 4500m, 4500m, 4500m, 4500m, 4500m ],
            [Material.Electronics] = [ 1400m, 1400m, 1400m, 1400m, 1400m, 1900m, 1900m, 1900m, 1900m, 1900m, 1900m, 1900m, 1900m ],
            [Material.Timber] =      [   50m,   50m,   55m,   55m,   60m,   60m,   60m,   60m,   60m,   55m,   55m,   50m,   50m ],
        };

        var cancellation = new Dictionary<CustomerTier, decimal>
        {
            [CustomerTier.SafetyCritical] = 0m,          // they wait: no alternative supplier
            [CustomerTier.EliteDeadline] = 0m,           // they wait, but miss the event
            [CustomerTier.StrategicDistributor] = 0.15m, // some switch to competitors (problem #3)
            [CustomerTier.Standard] = 0.30m,             // consumers and clubs give up quickly
        };

        PlanEvent[] events =
        [
            new(1, "New-year fitness peak"),
            new(4, "Alternative carbon fibre supplier qualified (+1,000 kg, P03)"),
            new(5, "Second electronics supplier (+500 kits) · 17 May ceremonial peak · Championships"),
            new(8, "Carbon fibre volume ramp-up (+500 kg)"),
            new(9, "Rowing boom starts to fade (C12)"),
        ];

        return new ForecastPlan(new DateOnly(2026, 12, 1), months, demand, supply, cancellation, events);
    }

    private static Scenario Build()
    {
        MaterialInfo[] materials =
        [
            new(Material.CarbonFibre, "Carbon fibre", "kg"),
            new(Material.Electronics, "Electronics", "kits"),
            new(Material.Timber, "Timber", "m³"),
        ];

        Product[] products =
        [
            // RØ Motion
            new("one", "RØ One", BusinessArea.Motion, 28_000m, Bom(cf: 3.0m, el: 1)),
            new("mini", "RØ Mini", BusinessArea.Motion, 12_000m, Bom(cf: 1.0m, el: 1)),
            new("pro", "RØ Pro", BusinessArea.Motion, 45_000m, Bom(cf: 5.0m, el: 2)),
            new("hytte", "RØ Hytte", BusinessArea.Motion, 22_000m, Bom(cf: 0.5m, el: 1, timber: 0.06m)),
            // RØ Marine
            new("comp-oar", "Competition oar pair", BusinessArea.Marine, 18_000m, Bom(cf: 2.4m)),
            new("boat", "Coastal rowing boat", BusinessArea.Marine, 95_000m, Bom(cf: 4.0m, timber: 0.8m)),
            new("rescue-oar", "Rescue oar set", BusinessArea.Marine, 6_000m, Bom(cf: 0.3m, timber: 0.04m)),
            new("ceremonial", "Ceremonial oar", BusinessArea.Marine, 15_000m, Bom(timber: 0.05m)),
            new("rec-oar", "Recreational oar pair", BusinessArea.Marine, 4_000m, Bom(timber: 0.03m)),
        ];

        Order[] orders =
        [
            new("M-101", "Distributor Nord", "one", 200, CustomerTier.StrategicDistributor, "Wants a binding date this week (E-01)"),
            new("M-102", "RowFit Germany", "one", 300, CustomerTier.StrategicDistributor, "New market entry, Germany"),
            new("M-103", "UK Fitness Partners", "pro", 150, CustomerTier.StrategicDistributor, "Gym chain roll-out"),
            new("M-104", "Webshop home customers", "mini", 600, CustomerTier.Standard, "Consumer pre-orders"),
            new("M-105", "Cabin retail chain", "hytte", 120, CustomerTier.Standard, "Summer season"),
            new("R-201", "Coastal Rescue Service", "rescue-oar", 400, CustomerTier.SafetyCritical, "Safety equipment"),
            new("R-202", "National rowing team", "comp-oar", 120, CustomerTier.EliteDeadline, "European Championships"),
            new("R-203", "Rowing clubs (12 clubs)", "comp-oar", 150, CustomerTier.Standard, "Club season"),
            new("R-204", "Rowing clubs (12 clubs)", "boat", 25, CustomerTier.Standard, "Club boats"),
            new("R-205", "Røyrvikholm municipality", "ceremonial", 40, CustomerTier.Standard, "Anniversary"),
            new("R-206", "Outdoor retailers", "rec-oar", 500, CustomerTier.Standard, "Summer season"),
        ];

        var supply = new Dictionary<Material, decimal>
        {
            [Material.CarbonFibre] = 3_000m,
            [Material.Electronics] = 1_400m,
            [Material.Timber] = 55m,
        };

        return new Scenario("Operation FULL FART – baseline month", "Baseline month", materials, products, orders, supply);
    }

    private static IReadOnlyDictionary<Material, decimal> Bom(decimal cf = 0, decimal el = 0, decimal timber = 0)
    {
        var bom = new Dictionary<Material, decimal>();
        if (cf > 0) bom[Material.CarbonFibre] = cf;
        if (el > 0) bom[Material.Electronics] = el;
        if (timber > 0) bom[Material.Timber] = timber;
        return bom;
    }
}
