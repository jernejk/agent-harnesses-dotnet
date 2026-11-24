## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes the discount table. The 1.4.0 change removed 1.3.0’s zero-discount fallback for non-gold tiers, so `standard` and other missing keys throw. Order 1001’s customer is `standard`, causing its total request to return 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this single line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

The replacement retains the table’s gold and new silver discounts, while restoring zero discount for every other tier, including runtime-null values. It leaves GST, rounding, and the other 1.4.0 changes untouched.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` after making the replacement. All **8 tests must pass**: standard pricing and rounding, gold and silver pricing, successful total responses for orders 1001–1003, and the unknown-order 404.

Before the fix, I reproduced **3 failures and 5 passes**. The post-fix run remains to be performed by the implementer. Do not use the live API for verification: it remains on the deployed build until redeployment.

## Risks

The existing tests do not cover arbitrary or null tiers, or assert the API response bodies. Their fallback behavior follows from the replacement but is not independently exercised by the current suite.