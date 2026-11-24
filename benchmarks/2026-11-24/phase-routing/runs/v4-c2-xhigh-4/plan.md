## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts`, which only defines `"gold"` and `"silver"` at lines 7–11. Commit `9bfbf7d` replaced the 1.3.0 fallback—5% for exact `"gold"`, 0% otherwise—with that direct lookup. The `"standard"` tier on order 1001 therefore throws, and the exception reaches the API response at `Program.cs:24`, producing HTTP 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16 only.

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

This is one find-and-replace in one file. It retains the lookup table’s intended 5% gold and 2% silver discounts, while returning 0% for any tier not in the table—as the prior release did. The null guard also avoids a null-key exception and preserves the old fallback behavior for a null tier.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The current baseline has **3 failures and 5 passes**; after the change, all 8 existing tests should pass. In particular:

- Standard-tier pricing returns 110.00 and the rounding test returns 21.89.
- Gold and silver pricing remain 104.50 and 107.80.
- Orders 1001, 1002, and 1003 return OK; the unknown-order test remains 404.

I did not apply the change, so these are expected post-fix results, not a post-fix test run. Verification should use the test suite, not the live API.

## Risks

Unmapped tiers will continue to receive 0% discount until explicitly added to the table; that matches the previous behavior. The deployed localhost service remains on 1.4.0 until redeployment, which is outside this plan.