## Root cause (file:line)

The findings are supported. In `src/Orders.Api/Pricing.cs:16`, `TierDiscounts[customer.Tier]` throws when a tier is absent from the dictionary, which contains only `gold` and `silver` (`Pricing.cs:7–11`). Order 1001’s customer has the `standard` tier (`OrderStore.cs:8`).

The cited commit, `9bfbf7d`, replaced the 1.3.0 behavior—5% for `gold`, 0% otherwise—with the dictionary indexer. The 2% silver discount is an intended 1.4.0 change; the missing-tier exception is the regression.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is a single-line replacement in one file. It preserves the table’s 5% gold and 2% silver discounts, while returning the previous 0% fallback for every tier not in the table—including `standard` and runtime null values. The rest of pricing and the intended 1.4.0 changes remain untouched.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite must pass all 8 tests, including:

- Standard pricing and rounding return their expected totals.
- Gold and silver pricing retain their expected discounts.
- API total requests for orders 1001, 1002, and 1003 return HTTP 200.
- Unknown orders still return 404.

I ran the suite before any fix: it reproduced the reported 3 failures and 5 passes. No fix was applied, so post-fix success is not yet verified. The deployed localhost service remains on 1.4.0 and is not part of this verification.

## Risks

Existing tests cover the `standard` fallback but do not individually exercise arbitrary unknown or null tiers; the proposed lookup handles those missing keys with the same zero-discount fallback. The repository is unchanged.