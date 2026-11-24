## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. Commit `9bfbf7d` replaced 1.3.0’s fallback—5% for `"gold"`, zero otherwise—with that lookup. Since `"standard"` is not a dictionary key, pricing throws `KeyNotFoundException`, causing the standard-tier tests and order 1001’s API test to fail. The silver discount added in 1.4.0 is intentional and should remain.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It changes one line in one file. Gold and silver still receive their configured discounts; every unlisted tier gets a zero discount, restoring the prior fallback. The null guard also preserves the old fallback behavior if a null tier reaches this method. The tax calculation and rounding remain unchanged.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` after applying the replacement. All **8 tests should pass**:

- Standard-tier list-price and rounding tests pass.
- Gold’s 5% and silver’s 2% discount tests continue to pass.
- API tests return 200 for orders 1001, 1002, and 1003, and 404 for the unknown order.

I ran the baseline `dotnet test` without changing files: it currently reports the three described failures and five passes. Verification should use the test suite, not the live API.

## Risks

The existing tests do not directly cover an arbitrary unconfigured tier or a null tier; the fallback is based on the 1.3.0 behavior. The deployed localhost service remains on 1.4.0 until redeployed; this repository change alone will not affect it.