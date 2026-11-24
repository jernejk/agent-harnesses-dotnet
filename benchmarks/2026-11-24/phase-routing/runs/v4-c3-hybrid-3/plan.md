## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes the discount dictionary directly. It throws for `"standard"` and any other unlisted tier; a null tier also throws. In v1.3.0, every non-gold tier, including null, received zero discount. The 1.4.0 silver discount is intentional.

## Change

In `src/Orders.Api/Pricing.cs`, make this **single-line replacement**:

Before:
```csharp
        var discount = TierDiscounts[customer.Tier];
```

After:
```csharp
        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var rate) ? rate : 0m;
```

## Why this is the smallest safe fix

The null guard and fallback restore zero discount for every unlisted tier, not just `"standard"`. The lookup retains the 1.4.0 gold and silver discounts without changing subtotal, GST or rounding behavior.

## Verification

After applying the replacement, run `dotnet test`. All **8 tests must pass**, including standard pricing and rounding, gold and silver pricing, all three order-total status checks, and the unknown-order 404 check.

The unmodified baseline reproduced **3 failures out of 8**. The fix has not been applied or tested.

## Risks

Existing tests do not cover null or arbitrary unknown tiers, and the API tests check status codes rather than total amounts. The deployed 1.4.0 service remains unchanged until redeployment, which is out of scope.