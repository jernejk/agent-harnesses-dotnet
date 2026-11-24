## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The lookup throws for any tier not present in the dictionary, including `standard`. The single `dotnet test` run reproduced the two pricing failures and the order 1001 API failure at that lookup.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

It is one line in one file. Configured discounts for `gold` and `silver` remain in use, while tiers without a configured discount fall back to zero, restoring the prior behavior for standard and other unmapped tiers.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests should pass, including the standard pricing and rounding tests and the API test for order 1001. Do not use the live API as verification: the deployed 1.4.0 service is unchanged without a redeploy.

## Risks

Unmapped tiers continue to receive no discount, as before. The test run confirms the reported standard-tier regression; it does not independently exercise every possible unmapped tier. The silver discount remains configured and is not changed.