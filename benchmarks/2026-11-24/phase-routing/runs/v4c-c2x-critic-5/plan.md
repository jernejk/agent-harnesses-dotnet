## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 contains only `"gold"` and `"silver"`, so `"standard"` throws `KeyNotFoundException`; the totals endpoint then returns HTTP 500.

The v1.3.0 code used a gold-or-zero conditional, so every non-gold tier received no discount. The 1.4.0 change intentionally adds silver’s 2% discount but accidentally removes that fallback. The reported cause is supported by the source and commit history.

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

It is one find-and-replace in one file. It preserves the table’s gold and silver discounts, and restores a zero-discount fallback for every other tier, including null—matching the old behavior without limiting the fix to `"standard"`.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`; the existing suite should pass **8/8**. In particular, standard pricing and rounding should pass, the API totals test for order 1001 should return OK, and the gold and silver pricing tests should continue to pass.

I ran `dotnet test` before any change: it reproduced the reported baseline of **3 failed, 5 passed**. No source files were modified. Verification should use the test suite, not the localhost API, which remains on the deployed 1.4.0 build until redeployment.

## Risks

Unknown or null tiers will continue to receive zero discount rather than fail. That restores v1.3.0 behavior; adding only a `"standard"` table entry would leave other previously accepted non-gold tiers failing. The test run will verify the repo change but will not update the deployed service.