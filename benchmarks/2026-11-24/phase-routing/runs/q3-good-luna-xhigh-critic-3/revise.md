## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The dictionary at lines 7–11 contains only `"gold"` and `"silver"`, so `"standard"` throws `KeyNotFoundException`.

Commit `9bfbf7d` replaced the earlier fallback—only gold received a discount; other tiers received zero—with the direct indexer. The seeded customer for order 1001 is standard-tier (`src/Orders.Api/OrderStore.cs:8,14`), which explains the API failure.

The null part of the critique is also valid. `Customer.Tier` is declared non-nullable, and the seeded store has no null tiers, but that annotation does not enforce a runtime invariant. `Pricing.Total` is callable with a `Customer` whose tier is null, and `TryGetValue(null, ...)` throws unless the null is handled first.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16.

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

This returns zero for both a null tier and a tier missing from the table, while using the configured value when a tier is present.

Add focused regression tests in `tests/Orders.Tests/PricingTests.cs` after the standard-tier test.

**Before**
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

Keep the existing gold and silver pricing tests: they already check that those configured discounts remain `104.50m` and `107.80m`, respectively.

## Why this is the smallest safe fix

It changes only the discount lookup statement and adds regression coverage for the unlisted- and null-tier cases. It restores the prior zero-discount fallback without adding a special-case entry for `"standard"` that would leave other unlisted tiers vulnerable to exceptions. The null guard is necessary because a dictionary lookup with a null key throws.

## Verification (what to run and what it must show)

After implementing the change, run:

```text
dotnet test
```

It must report **10 passed, 0 failed**: the current 8 tests plus the two new pricing regressions. The results must include passing standard, unlisted, and null tier pricing at `110.00m`; the existing gold and silver discount checks; the rounding test; and the API test for order 1001 returning HTTP 200.

The unchanged-checkout baseline is **3 failed, 5 passed**. Do not verify against the live API: the deployed 1.4.0 service will not receive this repository patch without a redeploy, which is out of scope.

## Risks

Null and unlisted tiers will receive no discount. That matches the former fallback behavior, including for null, but could conceal an unexpected tier value; rejecting unknown values would require a separate policy decision. The repository change alone will not alter the deployed service.