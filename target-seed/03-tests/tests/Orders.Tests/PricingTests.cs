using Orders.Api;

namespace Orders.Tests;

public class PricingTests
{
    static readonly Order HundredDollars = new(1, 1, [new("SKU-1", 2, 50.00m)]);
    static readonly Order OddCents = new(2, 1, [new("SKU-2", 1, 19.90m)]);

    static Customer Tier(string tier) => new(1, "Test", tier);

    [Fact]
    public void Total_adds_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
}
