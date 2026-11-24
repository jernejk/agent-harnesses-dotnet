## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` uses `TierDiscounts[customer.Tier]`. Commit `9bfbf7d` replaced the v1.3.0 behavior—5% off for `"gold"`, otherwise no discount—with a dictionary indexer containing only `"gold"` and `"silver"`. For `"standard"` and any other unlisted tier, the indexer throws `KeyNotFoundException`. That breaks the existing standard-tier pricing tests and makes `/orders/1001/total` return 500.

The report is supported by the source, history, and baseline test run: `dotnet test` currently has 3 failures and 5 passes. Silver’s 2% discount is an intended 1.4.0 change and should remain.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is a one-line replacement in one file. It keeps the lookup table and its intended gold and silver discounts, while restoring the prior zero-discount fallback for every tier that is not listed—not just `"standard"`. The null check also preserves the old comparison’s behavior if a null tier is passed at runtime, despite the model declaring `Tier` as non-nullable.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should pass all 8 tests, including:

- Standard-tier pricing and rounding tests pass.
- The silver-tier test still returns the expected 2%-discounted total.
- API totals for orders 1001, 1002, and 1003 all return HTTP 200.
- The unknown-order test still returns 404.

No tests need to be added. The baseline before the fix is 3 failed and 5 passed.

## Risks

The fallback means an unrecognized tier receives no discount, which could conceal a misspelled or newly introduced tier; that is the v1.3.0 behavior being restored. The localhost service remains on the deployed 1.4.0 build until redeployment, so this repo change alone will not alter its live behavior.