## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table at lines 7–11 contains `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`.

This matches the seeded data: `src/Orders.Api/OrderStore.cs:8` assigns customer 2 the `standard` tier, and line 14 maps order 1001 to that customer. Commit `9bfbf7d` introduced the failure by replacing the prior fallback—gold received 5% off; other tiers received 0%—with the direct indexer. Running `dotnet test` on the unchanged checkout reproduced the reported results: **3 failed, 5 passed**, including the two pricing failures and order 1001’s `InternalServerError`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

It changes one line and restores the existing zero-discount fallback for unlisted tiers, while preserving the configured gold and silver discounts. Adding only a `standard` entry would fix the known customer but would leave the direct-index failure for other unlisted tiers.

## Verification (what to run and what it must show)

After implementing the change, run:

```text
dotnet test
```

It must report **8 passed, 0 failed**. In particular, standard-tier pricing should be `110.00m` for the test’s $100 subtotal, the rounding test should pass, and the API test for order 1001 should receive HTTP 200.

The current unchanged-checkout baseline is **3 failed, 5 passed**. Do not use the live API for verification: the deployed 1.4.0 service will not receive a repository patch without a redeploy, which is out of scope.

## Risks

Unlisted tiers will continue to receive no discount, matching the prior fallback behavior. If the business policy instead requires rejecting unknown tiers, that would need a separate policy decision. This repository change alone will not alter the deployed service.