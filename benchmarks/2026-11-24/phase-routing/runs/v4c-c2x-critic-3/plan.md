## Root cause (file:line)

**Confirmed:** `src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly using `customer.Tier`. The 1.4.0 change in commit `9bfbf7d` added `"gold"` and `"silver"` to the table but replaced the earlier fallback with an indexer. Any tier absent from the table throws `KeyNotFoundException`; for order 1001, the customer’s `"standard"` tier causes the endpoint to return 500.

The parent version confirms that non-gold tiers received a zero discount. The new 2% silver discount is intentional and should remain.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this single line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is { } tier ? TierDiscounts.GetValueOrDefault(tier) : 0m;
```

## Why this is the smallest safe fix

This is one line in one file. It retains the table’s existing gold and silver discounts, while using zero for any tier not in the table. The null guard also preserves the old comparison’s zero-discount behavior if a null tier reaches this method. The total calculation, rounding, API behavior, and intentional silver-tier change remain untouched.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite must report **8 passed, 0 failed**, including:

- Standard pricing returns `110.00`, and standard-tier rounding returns `21.89`.
- The API total test returns OK for order 1001; the existing gold and silver API cases also remain OK.
- Gold pricing remains `104.50` and silver pricing remains `107.80`.
- The unknown-order test remains 404.

I ran `dotnet test` before any change: it reproduced the incident with **3 failures and 5 passes**—the two standard-pricing tests and the order 1001 API case. No code was changed, so post-fix results remain to be confirmed by the implementer.

## Risks

The existing tests do not directly cover arbitrary unrecognized tier values or null tiers, although the proposed fallback handles both. This repo patch will not affect the deployed localhost service until redeployment; live API verification is out of scope.