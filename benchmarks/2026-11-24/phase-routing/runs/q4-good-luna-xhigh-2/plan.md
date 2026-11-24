## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table contains `gold` and `silver`, but not `standard`, so standard-tier requests throw `KeyNotFoundException`.

The findings are supported by the code and commit history: before commit `9bfbf7d`, the code gave gold a 5% discount and every other tier zero; the commit added silver at 2% but replaced that fallback with the throwing indexer.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It changes only the lookup line. Gold and silver continue to use their configured discounts; any unlisted tier gets the dictionary’s default value of zero, restoring the old fallback. The null check also preserves the previous behavior for a null tier without passing a null key to the dictionary.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`—not the deployed localhost API. The existing suite should report **8 passed, 0 failed**, including standard pricing and rounding, gold and silver pricing, successful order-total responses for IDs 1001–1003, and the unknown-order 404.

I ran `dotnet test` before any change: it reported **3 failed, 5 passed**, with the two standard-pricing tests and order 1001 API test failing as described. I did not apply the fix, so the post-fix result remains to be verified by the implementer.

## Risks

The existing tests do not directly cover arbitrary unlisted tiers or a null tier; the proposed lookup handles both with the restored zero-discount behavior. The deployed 1.4.0 service will remain unchanged until redeployment, which is out of scope.