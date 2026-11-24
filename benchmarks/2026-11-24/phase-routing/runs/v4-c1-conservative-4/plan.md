## Root cause (file:line)

The finding is supported. In `src/Orders.Api/Pricing.cs:16`, `TierDiscounts[customer.Tier]` throws when the tier is absent from the dictionary, which contains only `gold` and `silver` (`Pricing.cs:7–11`). Order 1001 belongs to a `standard` customer (`OrderStore.cs:8`).

The regression commit `9bfbf7d` replaced the prior gold-or-zero fallback with that indexer. As a result, **any tier other than the listed keys** can throw—not just `standard`. The prior code gave every non-`gold` tier a zero discount.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace only line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is { } tier ? TierDiscounts.GetValueOrDefault(tier) : 0m;
```

## Why this is the smallest safe fix

This is a one-line replacement in one file. It preserves the intended `gold` and `silver` rates, while returning a zero discount for any unlisted tier. The null guard also preserves the previous expression’s zero-discount behavior if a null tier reaches the method.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` after implementing the change. The existing suite should pass **8/8 tests**, including:

- Standard pricing and cent-rounding expectations.
- The gold and silver discount expectations.
- HTTP 200 for totals on orders 1001, 1002, and 1003.
- HTTP 404 for an unknown order.

I ran `dotnet test` before any change as a baseline: **3 failed, 5 passed**, reproducing the two standard-pricing failures and the HTTP 500 for order 1001. Do not use the live API for verification: the deployed 1.4.0 instance will not receive this repo change without a redeploy.

## Risks

Low. Unknown tiers will again receive zero discount, as they did before 1.4.0; `gold` and the intended new `silver` rate remain unchanged. The tier lookup remains case-sensitive, as before.