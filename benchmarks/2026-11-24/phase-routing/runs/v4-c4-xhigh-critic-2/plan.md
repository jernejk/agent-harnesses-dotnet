## Root cause (file:line)

The reported cause is confirmed: `src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table contains only `gold` and `silver`, so a `standard` tier throws `KeyNotFoundException`.

That direct lookup was introduced by `9bfbf7d`. Its parent used a zero-discount fallback for every non-gold tier. Order 1001 is standard tier (`OrderStore.cs:8`), and the total endpoint calls `Pricing.Total` (`Program.cs:24`), so the exception produces the 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace only line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier != null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is one line in one file. It keeps the lookup table and its intended gold and silver discounts, while restoring the previous zero-discount fallback for any tier without a table entry. The null guard also preserves the old fallback behavior for a null tier value.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests must pass:

- Standard pricing returns 110.00 and the standard rounding test returns 21.89.
- Gold and silver pricing remain 104.50 and 107.80.
- The API total tests for orders 1001, 1002, and 1003 return OK; the unknown-order test still returns 404.

I ran `dotnet test` before any fix: it reproduced the reported 3 failures and 5 passes. I did not change files or test the deployed API.

## Risks

The existing tests cover `standard`, `gold`, and `silver`, but not arbitrary or null tier values. The proposed fallback preserves the prior behavior for those values; no additional tests are proposed.