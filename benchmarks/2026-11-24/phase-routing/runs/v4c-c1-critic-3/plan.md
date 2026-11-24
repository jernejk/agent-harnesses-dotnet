## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table contains `"gold"` and `"silver"`, but not `"standard"`, so the lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer.

The findings are supported by the code and history: commit `9bfbf7d` replaced the previous gold-only conditional—which gave every non-gold tier no discount—with the dictionary index. The silver discount is an intended 1.4.0 change.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace only this line:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

It changes only the failing lookup line. Gold and silver continue to use their 1.4.0 table discounts; any other tier gets the zero discount that the previous release applied to every non-gold tier. The null guard also avoids throwing if a null tier reaches this method at runtime. The existing subtotal, tax, and rounding calculations remain unchanged.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`; the existing suite should pass all 8 tests, including:

- Standard-tier pricing and rounding tests.
- Gold and silver discount tests.
- API totals for orders 1001, 1002, and 1003, including HTTP 200 for order 1001.
- The unknown-order test, which should continue to return 404.

The baseline run before the fix failed 3 tests and passed 5, with failures matching the reported missing-key behavior. No new tests are planned.

## Risks

Existing tests cover the standard-tier fallback and intended gold/silver discounts, but not arbitrary unknown tier strings or null; the fallback in the proposed line covers those cases. The deployed localhost service remains on 1.4.0 until redeployed, so this repository change would not affect it immediately. No files were changed.