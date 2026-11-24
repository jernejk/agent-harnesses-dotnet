## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The table contains `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer (`OrderStore.cs:8,14`).

Commit `9bfbf7d` replaced the previous zero-discount fallback for non-gold tiers with the direct lookup. I reproduced the reported baseline: **3 failed, 5 passed**.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

Before:
```csharp
        var discount = TierDiscounts[customer.Tier];
```

After:
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
            ? tierDiscount : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, extend the section following the standard-tier test:

Before:
```csharp
    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
```

After:
```csharp
    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Theory]
    [InlineData("unrecognized")]
    [InlineData(null)]
    public void Unlisted_or_null_tiers_pay_list_price_plus_gst(string? tier) =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier!)));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
```

The null-forgiving operator deliberately tests the legacy behavior for a null tier despite the model’s non-nullable declaration.

## Why this is the smallest safe fix

The lookup change preserves gold and silver discounts while restoring the previous zero-discount fallback for standard, unlisted, and null tiers. Adding only `standard` to the table would leave other previously accepted tiers throwing. Existing tests cover standard pricing, rounding, both discounted tiers, and the API status; the two new cases guard the fallback.

## Verification (what to run and what it must show)

After implementing the changes, run `dotnet test` from the repo root. It should report **10 passed, 0 failed**, including HTTP 200 from the in-process API test for order 1001 and the new fallback cases.

The patch and post-fix test run have not been performed. Do not use the live API for patch verification: it remains on the deployed 1.4.0 build until a redeploy.

## Risks

- Unlisted or null tiers silently receive no discount, matching the pre-1.4.0 behavior but potentially concealing bad tier data.
- Tier matching remains case-sensitive. The existing API test checks status, not the returned total.