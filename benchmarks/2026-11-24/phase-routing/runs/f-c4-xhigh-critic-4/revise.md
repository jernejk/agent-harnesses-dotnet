## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 contains only `"gold"` and `"silver"`. Order 1001 maps to a `"standard"` customer (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException`. The endpoint calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`.

Commit `9bfbf7d` introduced the lookup and removed the previous fallback, `customer.Tier == "gold" ? 0.05m : 0m`. That fallback gave zero discount to every tier other than gold, not just `"standard"`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, keep the dictionary and its gold/silver rates unchanged. Replace the throwing indexer with a lookup that uses zero discount when the tier is absent:

**Before (`Pricing.cs:16`):**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After:**
```csharp
        var discount = customer.Tier is not null &&
                       TierDiscounts.TryGetValue(customer.Tier, out var configuredDiscount)
            ? configuredDiscount
            : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, the standard, gold, and silver cases already exist. Add regression coverage for an unrecognized tier and for runtime null as defensive preservation of the previous fallback:

```csharp
    [Fact]
    public void Unrecognized_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("bronze")));

    [Fact]
    public void Null_tier_uses_zero_discount_fallback() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(null!)));
```

`Customer.Tier` is declared as non-nullable `string` (`src/Orders.Api/Models.cs:3`), so null is outside the stated contract. The null test deliberately exercises the old fallback’s behavior if a null nevertheless reaches pricing.

## Why this is the smallest safe fix

It restores the previous zero-discount behavior for every tier that is not gold or silver, while retaining the configured gold and silver discounts. It avoids a special-case fix for only `"standard"` and doesn’t change the customer model or API contract. The two added tests cover the missing fallback behavior; existing tests continue to cover standard, gold, and silver pricing.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

The current baseline reproduces 3 failures out of 8 tests: two standard-tier pricing tests throw `KeyNotFoundException`, and the order-1001 API test returns `InternalServerError` instead of `OK`. After the change and the two added regression tests, the suite should pass all 10 tests. It must show the unrecognized-tier and null fallback tests passing, existing pricing tests passing, and the order-1001 API test returning `OK`.

Do not use localhost API behavior as verification: it remains the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

Unrecognized tiers—including a runtime null—will receive zero discount rather than causing the lookup to throw. That restores the prior behavior but could conceal unexpected tier values. The null test covers defensive behavior; null remains outside the declared non-nullable customer contract. The deployed service will continue to show the incident until a fixed build is deployed.