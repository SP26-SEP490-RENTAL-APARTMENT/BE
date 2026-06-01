namespace BLL.Services.Implements;

internal static class OccupancyPricingFormula
{
    private const decimal BaselineOccupancy = 0.55m;
    private const decimal Sensitivity = 0.8m;
    private const decimal MinimumMultiplier = 0.85m;
    private const decimal MaximumMultiplier = 1.35m;

    public static decimal CalculateMultiplier(decimal occupancyRate)
    {
        var delta = occupancyRate - BaselineOccupancy;
        var adjustment = delta * Sensitivity;

        return Math.Clamp(1m + adjustment, MinimumMultiplier, MaximumMultiplier);
    }

    public static string DescribeMultiplier(decimal occupancyRate, decimal multiplier)
    {
        return $"occFactor=clamp(1+(({occupancyRate:F2}-{BaselineOccupancy:F2})x{Sensitivity:F2}),{MinimumMultiplier:F2},{MaximumMultiplier:F2})={multiplier:F4}";
    }
}