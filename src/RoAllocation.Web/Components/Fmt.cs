using System.Globalization;
using RoAllocation.Core;

namespace RoAllocation.Web.Components;

/// <summary>Display formatting. SVG coordinates always use the invariant culture.</summary>
public static class Fmt
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

    public static string Nok(decimal v) => $"NOK {(v / 1_000_000m).ToString("0.0", En)} M";
    public static string Num(decimal v) => v.ToString("#,0.#", En);
    public static string Num(int v) => v.ToString("#,0", En);
    public static string Pct(double v) => v.ToString("P0", En);
    public static string Svg(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Name(AllocationStrategy s) => s switch
    {
        AllocationStrategy.MaxRevenue => "Max revenue",
        AllocationStrategy.CustomerCriticalFirst => "Critical customers first",
        AllocationStrategy.ProRata => "Fair share (pro rata)",
        _ => s.ToString(),
    };

    public static string Description(AllocationStrategy s) => s switch
    {
        AllocationStrategy.MaxRevenue =>
            "Gives material to the orders that earn the most per kilo of scarce material. Ignores who the customer is.",
        AllocationStrategy.CustomerCriticalFirst =>
            "Safety equipment first, then elite event deadlines, then strategic distributors, then everyone else.",
        AllocationStrategy.ProRata =>
            "Every order gets the same share of what it asked for. Nobody wins, nobody is left with nothing.",
        _ => "",
    };

    public static string Name(CustomerTier t) => t switch
    {
        CustomerTier.SafetyCritical => "Safety-critical",
        CustomerTier.EliteDeadline => "Elite deadline",
        CustomerTier.StrategicDistributor => "Strategic distributor",
        _ => "Standard",
    };

    public static string Css(CustomerTier t) => t switch
    {
        CustomerTier.SafetyCritical => "tier-safety",
        CustomerTier.EliteDeadline => "tier-elite",
        CustomerTier.StrategicDistributor => "tier-strategic",
        _ => "tier-standard",
    };

    public static string Css(BusinessArea a) => a == BusinessArea.Motion ? "area-motion" : "area-marine";

    public static string Css(FulfilmentStatus s) => s switch
    {
        FulfilmentStatus.Full => "ok",
        FulfilmentStatus.Partial => "warn",
        _ => "risk",
    };

    public static string Label(FulfilmentStatus s) => s switch
    {
        FulfilmentStatus.Full => "Full",
        FulfilmentStatus.Partial => "Partial",
        _ => "Not covered",
    };

    public static string Css(InsightSeverity s) => s switch
    {
        InsightSeverity.Critical => "risk",
        InsightSeverity.Warning => "warn",
        _ => "info",
    };
}
