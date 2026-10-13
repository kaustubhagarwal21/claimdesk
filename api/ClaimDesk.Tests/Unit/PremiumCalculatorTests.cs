using ClaimDesk.Api.Domain;

namespace ClaimDesk.Tests.Unit;

public class PremiumCalculatorTests
{
    [Theory]
    [InlineData(PolicyType.Motor, 2.5)]
    [InlineData(PolicyType.Health, 4.0)]
    [InlineData(PolicyType.Home, 0.8)]
    public void Each_policy_type_has_its_yearly_rate(PolicyType type, double expectedPercent)
    {
        Assert.Equal((decimal)expectedPercent, PremiumCalculator.RatePercent(type));
    }

    [Theory]
    [InlineData(PolicyType.Motor, 500_000, 12_500)]
    [InlineData(PolicyType.Health, 300_000, 12_000)]
    [InlineData(PolicyType.Home, 2_500_000, 20_000)]
    public void Premium_is_sum_insured_times_rate(PolicyType type, int sumInsured, int expectedPremium)
    {
        Assert.Equal(expectedPremium, PremiumCalculator.Calculate(type, sumInsured));
    }

    [Fact]
    public void Premium_is_rounded_to_two_decimals()
    {
        // 12345.67 x 0.8% = 98.76536
        Assert.Equal(98.77m, PremiumCalculator.Calculate(PolicyType.Home, 12_345.67m));
    }

    [Fact]
    public void An_unknown_policy_type_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PremiumCalculator.RatePercent((PolicyType)99));
    }
}
