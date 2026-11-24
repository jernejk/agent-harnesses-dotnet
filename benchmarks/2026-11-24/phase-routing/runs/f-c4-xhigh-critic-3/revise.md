## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–11` defines discounts only for `gold` and `silver`; line 16 indexes the dictionary with `customer.Tier`. For the standard-tier customer in `src/Orders.Api/OrderStore.cs:8`, order 1001 reaches that lookup through `src/Orders.Api/Program.cs:23–24` and throws `KeyNotFoundException`, resulting in HTTP 500.

Git blame attributes the dictionary and indexed lookup to `9bfbf7d8`. The parent implementation gave a discount only to `gold` and used zero for every other tier. So the review’s general zero-fallback suggestion is consistent with the previous behavior; adding only a `standard` entry would not preserve it for other unlisted tiers.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace the direct index with a lookup that defaults to zero. The null guard preserves the prior behavior for a runtime null as well: `Customer.Tier` is declared non-nullable, but the previous conditional treated null as non-gold.

**Before:**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After:**
```csharp
        var discount = customer.Tier is { } tier &&
                       TierDiscounts.TryGetValue(tier, out var tierDiscount)
            ? tierDiscount
            : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, add a test for an unlisted tier and one for null:

**Before:**
```csharp
    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));

    [Fact]
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
```

**After:**
```csharp
    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));

    [Fact]
    public void Unlisted_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("platinum")));

    [Fact]
    public void Null_tier_pays_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", null!)));

    [Fact]
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
```

Keep the existing `gold` and `silver` dictionary entries and rates unchanged.

## Why this is the smallest safe fix

The lookup change preserves both existing discounts while restoring the old zero-discount behavior for standard and other unlisted tiers. It also preserves that behavior for runtime null values without changing the `Customer` model. The two added tests cover the fallback cases; the existing pricing and API tests continue to cover known tiers, rounding, and endpoint behavior.

## Verification (what to run and what it must show)

Run `dotnet test` from the repository root. The current baseline is **3 failed, 5 passed, 8 total**; after the two new pricing tests, the suite should show **10 passed, 0 failed**.

The tests must show that:
- Standard, unlisted, and null tiers get no discount; the standard-price example totals `$110.00`.
- Gold and silver retain their existing expected totals, and rounding still passes.
- All three order-total API cases pass, including order 1001, and the unknown-order case still returns 404.

The API tests already use `WebApplicationFactory<Program>.CreateClient()` and the test project references `src/Orders.Api/Orders.Api.csproj`; they exercise the built API in a test host, not deployed localhost. Do not use the live API for verification: localhost remains on 1.4.0, and redeployment is out of scope.

## Risks

Defaulting unknown tiers to zero can mask an unexpected tier value or spelling error. This is an intentional compatibility choice based on the prior implementation. Null is declared outside the model’s non-null contract, but this plan preserves its previous zero-discount behavior. No code was changed.