## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes the discount table directly. It contains `gold` and `silver` but not `standard`, so order 1001 throws `KeyNotFoundException` and the route returns HTTP 500.

In 1.3.0, every tier other than `gold`, including a null tier, received zero discount. The 1.4.0 lookup lost that fallback.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This single replacement restores the zero-discount fallback for missing and null tiers, not just `standard`. It retains the 1.4.0 lookup and silver discount, as well as the existing gold discount, GST and rounding.

## Verification

After making the replacement, run `dotnet test`. All **8 tests should pass**: standard pricing and rounding, gold and silver pricing, totals endpoints for orders 1001–1003, and the unknown-order 404.

The unmodified baseline produced 3 failures and 5 passes. The proposed fix has **not** been applied or tested.

## Risks

Existing tests do not cover null or other unknown tiers, and the API tests check status codes rather than total amounts. The local live API remains on the deployed 1.4.0 build until a redeploy, which is out of scope.