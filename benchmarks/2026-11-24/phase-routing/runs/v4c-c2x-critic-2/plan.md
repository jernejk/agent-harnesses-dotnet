## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table contains only `gold` and `silver`, so `standard`—and any other unlisted tier—throws `KeyNotFoundException`. This matches the reported 500 for order 1001.

The parent of commit `9bfbf7d` used a conditional that gave 5% off only to exact `gold` and 0% to every other tier. The commit intentionally added a 2% discount for `silver`, but the direct index lost that fallback behavior.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier ?? string.Empty);
```

## Why this is the smallest safe fix

This is one replacement in one file. The dictionary retains both intended discounts: 5% for `gold` and 2% for `silver`. Missing tiers default to `0m`, restoring the previous behavior for standard and all other unlisted tiers. The null fallback also preserves the prior conditional’s no-discount behavior for a null tier.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`; the existing suite must pass **all 8 tests, with 0 failures**. In particular, standard-tier pricing and rounding must pass, the API tests for orders 1001, 1002, and 1003 must receive 200, and the unknown-order test must still receive 404. The current baseline, before any change, is 3 failed and 5 passed.

## Risks

Unlisted tiers continue to receive no discount, matching the prior release; this also means invalid tier values are not rejected. No tests or code were changed, and the deployed 1.4.0 service was not used for verification.