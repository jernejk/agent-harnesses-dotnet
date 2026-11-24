## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`. Commit `9bfbf7d` replaced the previous `gold`-only conditional—which gave every other tier a zero discount—with a direct dictionary index. The dictionary contains `gold` and `silver`, but no `standard`, so looking up order 1001’s customer throws `KeyNotFoundException`. Any other unmapped tier can fail similarly; a runtime null tier would also throw, whereas the old conditional treated it as non-gold.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

This is one find-and-replace in one file. Leave the discount table and the rest of `Total` unchanged.

## Why this is the smallest safe fix

It retains the 1.4.0 discounts for `gold` and `silver`, while restoring the previous zero-discount fallback for every tier without a table entry, including `standard` and runtime null. It changes only the failing lookup line and preserves the existing subtotal, GST, and rounding behavior.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing 8 tests should all pass: standard-tier pricing and rounding, gold and silver pricing, successful API totals for orders 1001–1003, and the unknown-order 404.

The current baseline run, before any fix, fails 3 of 8 tests: standard pricing, rounding, and the API total for order 1001. I did not apply the fix or run a post-fix test. As requested, verification should use the test suite, not the live API.

## Risks

Unmapped or invalid tiers will continue to receive zero discount rather than raising an exception; that matches the prior behavior. The tests cover the reported standard-tier failure and the intended gold/silver pricing, but do not separately exercise every unmapped tier or a runtime null. The localhost deployment remains on 1.4.0 until redeployed; no code was changed here.