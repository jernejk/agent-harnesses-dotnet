## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 contains only `gold` and `silver`, so `standard`—and any other unlisted tier—throws `KeyNotFoundException`.

Order 1001 belongs to the `standard` customer (`src/Orders.Api/OrderStore.cs:8`), and the total endpoint calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`. Commit `9bfbf7d` introduced the lookup, replacing the prior behavior that gave every non-gold tier zero discount.

## Change (the exact lines before and after)

Replace the direct lookup with a lookup that uses zero when the tier is unlisted and avoids looking up a null key:

**Before** — `src/Orders.Api/Pricing.cs:16`
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

Keep the existing `gold` and `silver` dictionary entries and the pricing calculation unchanged.

Add a regression test for an unlisted tier in `tests/Orders.Tests/PricingTests.cs`:

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
    public void Unknown_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("bronze")));

    [Fact]
    public void Totals_round_to_cents() =>
```

The null-specific test suggested by the critique is not required under the current contract: `Customer.Tier` is declared as non-nullable `string` (`src/Orders.Api/Models.cs:3`), with nullable reference types enabled. The production change still safely defaults to zero if a null tier reaches it at runtime.

## Why this is the smallest safe fix

This restores the previous zero-discount fallback for arbitrary unlisted tier strings while retaining the explicit gold and silver discounts. It avoids both the incident for `standard` and the same failure for other unlisted values, without changing the endpoint, model, or pricing calculation.

The added `bronze` test verifies the general fallback rather than only adding a special case for `standard`. Existing tests continue to cover standard, gold, silver, rounding, and the API totals for orders 1001–1003.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

After the change, it must pass all **9 tests** with **0 failures**, including the new unknown-tier regression test and the existing standard-tier and API total tests.

I ran `dotnet test` before the change: **3 failed, 5 passed** out of 8. The failures were the standard pricing tests and the order 1001 API total test. Do not verify against the live API: the localhost service is the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

Unlisted values, including an unexpected tier spelling, will receive no discount. That preserves the prior behavior for non-gold tiers and is the requested fallback policy; new discounted tiers must be explicitly added to the table. Null is outside the declared model contract, although this implementation defensively defaults it to zero. No files were changed during this investigation.