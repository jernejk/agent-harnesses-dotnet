### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table contains `"gold"` and `"silver"` but not `"standard"`, so `Pricing.Total` throws `KeyNotFoundException` for standard-tier customers. Order 1001 uses customer 2, whose tier is standard, and the total endpoint calls this method.

The parent version confirms the prior fallback gave zero discount to every tier other than exact `"gold"`. The 1.4.0 change intended to add silver pricing, but replaced that fallback with the throwing indexer.

### Change (the exact lines before and after)

Replace only line 16 in `src/Orders.Api/Pricing.cs`:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

### Why this is the smallest safe fix

It is a one-line, one-file replacement. Missing tiers default to zero, restoring the previous behavior for standard and other unmapped tiers; the null guard also preserves the old behavior if a null tier reaches this method. Gold and silver still use their existing table discounts, so the intended 1.4.0 pricing change remains intact.

### Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should pass all 8 tests: standard pricing and rounding, gold and silver discounts, all three seeded order-total API cases, and the unknown-order 404 case.

I ran `dotnet test` before any fix: it reproduced the reported **3 failed, 5 passed**, with failures in the two standard-tier pricing tests and the order 1001 API case. No fix was applied.

### Risks

Unmapped tier names will continue to receive zero discount, matching the previous fallback; a future intended discount will need an explicit table entry. The existing suite does not directly test null or arbitrary unknown tiers. The deployed 1.4.0 service will remain unchanged until a redeploy, which is outside this plan.