## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 has entries for `gold` and `silver`, but not `standard`, so standard-tier pricing throws `KeyNotFoundException`.

The evidence supports the findings: in v1.3.0, only `gold` received a discount and every other tier received zero. Commit `9bfbf7d` added silver but replaced that fallback with the throwing lookup. The existing tests still expect standard customers to pay list price plus GST. The endpoint passes the stored customer to `Pricing.Total` at `src/Orders.Api/Program.cs:24`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16 only:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is one find-and-replace in one file. It keeps the 1.4.0 table-driven gold and silver discounts, while restoring the previous zero-discount fallback for every tier not in the table. The null guard also preserves the prior equality-check behavior if a runtime null tier is supplied; a direct `GetValueOrDefault` lookup would still throw for a null key.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The current baseline is 3 failures and 5 passes; after the change, the existing suite should pass **all 8 tests**:

- Standard pricing and standard rounding tests pass.
- Gold and silver pricing tests pass at their expected discount rates.
- Endpoint requests for orders 1001, 1002, and 1003 all return OK.
- The unknown-order test still returns 404.

No new tests or live API checks are planned.

## Risks

Unknown tiers continue to receive zero discount, as they did in 1.3.0; this preserves behavior but can conceal an unexpected tier value. The deployed 1.4.0 service will not reflect this repository change until redeployed, which is out of scope. No files were changed.