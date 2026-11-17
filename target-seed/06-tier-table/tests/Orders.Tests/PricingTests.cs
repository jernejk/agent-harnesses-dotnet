using Orders.Api;

namespace Orders.Tests;

public class PricingTests
{
    static readonly Order HundredDollars = new(1, 1, [new("SKU-1", 2, 50.00m)]);
    static readonly Order OddCents = new(2, 1, [new("SKU-2", 1, 19.90m)]);

    static Customer Tier(string tier) => new(1, "Test", tier);

    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
        Assert.Equal(104.50m, Pricing.Total(HundredDollars, Tier("gold")));

    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));

    [Fact]
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
}
