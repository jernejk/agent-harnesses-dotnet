## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` — introduced by commit `9bfbf7d8` (`Move tier discounts into a lookup table and add silver tier`). `Total` indexes `TierDiscounts` directly, but the table has entries only for `gold` and `silver`. A `standard` tier therefore throws `KeyNotFoundException`.

The parent version applied a discount only when the tier was `gold`; all other tiers received no discount. The regression affects any tier not in the new table, not just `standard`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is one line in one file. Known tiers keep their table-defined discounts: gold remains 5% and silver remains 2%. Missing or null tiers receive no discount, restoring the prior behavior without changing the GST calculation, rounding, or the intentional silver-tier change.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should pass all 8 tests, including:

- Standard-tier total of `110.00` and the standard-tier rounding result of `21.89`.
- Gold total of `104.50` and silver total of `107.80`.
- HTTP OK for the existing order-total checks for orders 1001, 1002, and 1003; unknown orders still return 404.

Before the fix, `dotnet test` reproduced 3 failures and 5 passes. Verification is against the repo tests, not the deployed localhost service.

## Risks

The existing tests don’t explicitly cover arbitrary or null tiers, though the proposed fallback handles them. Any tier not added to the table will continue to receive no discount, matching the previous behavior. The running 1.4.0 service won’t receive this repo change without a redeploy, which is out of scope. No files were changed.