## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table contains only `"gold"` and `"silver"`, but order `1001` belongs to a `"standard"` customer. That lookup throws `KeyNotFoundException`, which makes the total endpoint return HTTP 500.

This regression was introduced by commit `9bfbf7d` (“Move tier discounts into a lookup table and add silver tier”), which replaced the earlier “gold gets 5%; otherwise zero” behavior with the dictionary indexer.

The Problem Details explanation is not supported: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`. The test run also confirms the pricing failure directly.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace the indexer with a null-safe lookup that defaults any unlisted tier to zero discount.

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null &&
                       TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
            ? tierDiscount
            : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, add tests for an unlisted tier and a runtime-null tier.

**Before** (context)
```csharp
    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
```

**After**
```csharp
    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Unlisted_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("platinum")));

    [Fact]
    public void Null_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", null!)));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
```

Keep the existing gold and silver pricing tests unchanged. Although `Customer.Tier` is annotated non-nullable and the current store has no null tier, the record constructor does not enforce that at runtime, and `Pricing.Total` is public. The null test exercises that reachable input safely.

## Why this is the smallest safe fix

It changes only the discount lookup: gold and silver retain their current discounts, while every unlisted tier—and null—gets the existing zero-discount behavior. It avoids a standard-only dictionary entry, which would leave other unlisted tiers vulnerable to the same exception. The existing calculation and endpoint behavior do not need to change.

## Verification (what to run and what it must show)

Run `dotnet test` after implementing the changes. The current unchanged checkout produced **3 failed, 5 passed**; failures included the `KeyNotFoundException` at `Pricing.cs:16` and the HTTP 500 for order `1001`. After the change, expect **10 passed, 0 failed**: the two added pricing tests should pass, existing standard/gold/silver pricing checks should remain green, order `1001` should return 200, and the unknown-order test should still return 404.

Do not use the localhost API for verification: it is still running the deployed 1.4.0 build, and redeployment is out of scope. The test run above was against the unchanged checkout; the post-change run remains for the implementing engineer.

## Risks

Defaulting unknown tiers to zero discount means a misspelled or newly introduced tier will be treated as list-price pricing rather than reported as an error. That is the requested fallback policy. The lookup remains case-sensitive, so differently cased tier names are also unlisted and receive zero discount.