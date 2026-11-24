## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table contains `gold` and `silver`, but not `standard`, so standard-tier requests throw `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer.

This matches the history: commit `9bfbf7d` replaced the 1.3.0 behavior—5% off for gold and zero for every other tier—with the throwing lookup.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace only this line:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It is a one-line replacement in one file. The lookup table remains intact, so gold and the intended 1.4.0 silver discount still apply. Unknown non-null tiers default to zero, restoring the old fallback behavior. The null guard also preserves the old behavior for a null tier; an unguarded dictionary lookup could still throw for that input.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should pass **8/8**: standard pricing and rounding, gold and silver pricing, successful totals for orders 1001–1003, and 404 for an unknown order.

I ran the suite before any change: it reproduces the reported **3 failures and 5 passes**. No new tests or code changes were made. Verification should use `dotnet test`, not the deployed localhost API.

## Risks

The existing tests do not explicitly cover null or arbitrary unknown tier values, though the proposed fallback handles both. Until a redeploy—which is out of scope—the deployed 1.4.0 service will remain unchanged.