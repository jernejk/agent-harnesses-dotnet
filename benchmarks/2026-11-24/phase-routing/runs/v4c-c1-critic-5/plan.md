## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table contains only `"gold"` and `"silver"` (lines 9–10), so a `"standard"` customer throws `KeyNotFoundException`. `OrderStore.cs` confirms order 1001 belongs to a standard-tier customer.

The findings are supported by the code and history: in v1.3.0, pricing applied the gold discount only to exact `"gold"` and used zero discount otherwise. Commit `9bfbf7d` retained the discount table but replaced that fallback with direct indexing.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier ?? string.Empty);
```

## Why this is the smallest safe fix

This is a one-line, one-file replacement. It preserves the table’s intended 5% gold and 2% silver discounts while returning the decimal default (`0m`) for tiers absent from the table, restoring the prior fallback for standard and any other unmatched tier. Normalizing a null tier to an empty key also avoids a null-key exception and follows the old zero-discount fallback.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`; all 8 existing tests must pass. In particular:

- Standard pricing returns the list price plus GST (`110.00m`), and standard rounding remains `21.89m`.
- Gold and silver pricing remain `104.50m` and `107.80m`, respectively.
- The total endpoint returns OK for orders 1001, 1002, and 1003; unknown orders still return 404.

The unmodified baseline currently reports 3 failed and 5 passed. No fix was applied, so post-fix results remain to be verified by the implementer. The deployed service was not used for verification.

## Risks

The existing tests cover standard, gold, and silver, but not arbitrary unmatched or null tiers. The proposed fallback handles those without changing the discount table or the intended silver behavior.