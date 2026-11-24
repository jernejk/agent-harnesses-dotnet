## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 contains only `"gold"` and `"silver"`, so `"standard"`—and any other unlisted tier—throws `KeyNotFoundException`.

The pre-regression code gave a discount only to exact `"gold"` and zero discount to every other tier. The reported order 1001 uses the standard-tier customer, so the exception propagates through the total endpoint as HTTP 500. The findings are supported, but adding only a `"standard"` entry would not restore behavior for every previously accepted tier.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is a one-line replacement that keeps the 1.4.0 lookup table and its intended gold and silver discounts, while restoring the previous zero-discount fallback for every other tier. The null guard also preserves the old comparison’s behavior if a null tier reaches this method at runtime.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` after the replacement. The existing suite should pass all 8 tests, including standard-tier pricing and rounding, gold and silver pricing, all three order-total endpoints, and unknown-order handling.

I ran `dotnet test` before any change: it reproduced the reported result of 3 failed and 5 passed. I did not apply the fix, so post-fix success remains to be verified by the implementer. No live API check is needed or appropriate: localhost is still running the deployed 1.4.0 build.

## Risks

The existing tests do not directly cover arbitrary unlisted tier values; the fallback for those is established by the replacement’s logic. As before, unrecognized tier values receive no discount rather than failing, which can hide a misspelled tier.