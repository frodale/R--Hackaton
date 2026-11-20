namespace RoAllocation.Core;

public interface IScenarioRepository
{
    Scenario GetCurrent();
}

/// <summary>
/// Mock data inspired by the RØ case (E-01, E-02, R01, R02). Prices, bills of material, quantities and
/// supply are our own assumptions for the demo; replace this class with real storage later.
/// </summary>
public sealed class InMemoryScenarioRepository : IScenarioRepository
{
    private static readonly Scenario Current = Build();

    public Scenario GetCurrent() => Current;

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

        return new Scenario("Operation FULL FART – June allocation", "June", materials, products, orders, supply);
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
