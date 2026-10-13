namespace ClaimDesk.Api.Domain;

// Premium = sum insured x yearly rate for the policy type.
public static class PremiumCalculator
{
    public static decimal RatePercent(PolicyType type) => type switch
    {
        PolicyType.Motor => 2.5m,
        PolicyType.Health => 4.0m,
        PolicyType.Home => 0.8m,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown policy type."),
    };

    public static decimal Calculate(PolicyType type, decimal sumInsured) =>
        Math.Round(sumInsured * RatePercent(type) / 100m, 2, MidpointRounding.AwayFromZero);
}
