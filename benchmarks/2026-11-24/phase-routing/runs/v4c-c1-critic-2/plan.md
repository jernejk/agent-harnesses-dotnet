## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The dictionary contains `"gold"` and `"silver"`, but not `"standard"`, so standard-tier requests throw. The `9bfbf7d` diff confirms this replaced the previous gold-only conditional, which gave every non-gold tier a zero discount. The direct lookup also changes null-tier behavior: the old comparison fell back to zero, while a dictionary lookup with a null key can throw.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier ?? string.Empty);
```

## Why this is the smallest safe fix

It is a one-line replacement in one file. Recognized tiers retain their configured discounts, including the intended 2% silver discount. Missing tiers—and a null tier—default to zero discount, restoring the previous behavior for non-gold inputs without changing subtotal calculation, GST, or rounding.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`; all 8 existing tests should pass. In particular, standard-tier pricing and rounding should pass, as should the API success case for order 1001. The existing gold and silver pricing tests should continue to pass. No new tests or live API checks are part of this plan.

On the untouched checkout, `dotnet test` currently fails 3 of 8 tests: the two standard-tier pricing tests and the API test for order 1001. The repository status was clean after this check.

## Risks

Unknown tier names will receive no discount rather than being rejected. That matches the last good release’s fallback behavior. The existing tests cover the reported standard-tier case and the intended gold/silver discounts, but do not separately test arbitrary unknown or null tiers. The deployed localhost service remains on 1.4.0 until redeployed; redeploy is out of scope.