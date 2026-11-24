## Root cause (file:line)

Confirmed: `src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table at lines 7–10 contains only `gold` and `silver`, so a `standard` tier throws `KeyNotFoundException`.

`OrderStore.cs:8` assigns order 1001’s customer the `standard` tier, and `Program.cs:24` calls `Pricing.Total` for the total endpoint. The history confirms commit `9bfbf7d` replaced the earlier zero-discount fallback for non-gold tiers with the dictionary lookup.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier != null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, add two tests after the silver-pricing test and before the rounding test.

**Before**
```csharp
    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));

    [Fact]
    public void Totals_round_to_cents() =>
```

**After**
```csharp
    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));

    [Fact]
    public void Unlisted_tier_gets_no_discount() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("platinum")));

    [Fact]
    public void Null_tier_gets_no_discount() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(null!)));

    [Fact]
    public void Totals_round_to_cents() =>
```

`null!` deliberately passes a runtime null through the existing non-nullable test helper and model property to exercise the guard.

## Why this is the smallest safe fix

The production change is one line in one file. It preserves the lookup table and its gold and silver discounts while restoring the previous zero-discount fallback for unlisted tiers. The null check also preserves that fallback for a runtime null tier.

The two added pricing tests cover the fallback behavior explicitly without changing the customer model or API contract.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

After implementation, it must pass all 10 test cases: the existing 8 plus the two new fallback tests. In particular, the new tests must produce a total of `110.00m` for an unlisted tier and a null tier; existing tests must retain the standard, gold, silver, and rounding results, and the API tests must retain their expected success and 404 responses.

The API tests use `WebApplicationFactory<Program>` and the test project references `src/Orders.Api`, so they exercise the repo-built application in-process—not the deployed localhost service. No redeploy or live API check is needed or included.

## Risks

The customer model declares `Tier` as non-nullable, but the null test checks that pricing remains defensive if a runtime null is supplied. Any unlisted tier will receive zero discount; changing that policy to validation or rejection would be a separate, broader change.