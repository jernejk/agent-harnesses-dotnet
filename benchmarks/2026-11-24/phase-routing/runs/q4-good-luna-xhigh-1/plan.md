## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts`, whose entries at lines 7–11 cover only `"gold"` and `"silver"`. For any other tier, the lookup throws `KeyNotFoundException`; a null tier would also throw.

This regresses the parent release’s behavior: only `"gold"` received a discount, and every other tier received zero. Order 1001 belongs to a standard-tier customer, so the same exception makes its total endpoint return HTTP 500. The reported `dotnet test` failures are reproducible: **3 failed, 5 passed**.

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

This is a one-line change in one file. It preserves both intended lookup-table discounts—5% for gold and 2% for silver—while falling back to zero for every unlisted tier, as the previous release did. The null guard also preserves the prior zero-discount behavior for a null tier instead of passing null to the dictionary lookup.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should pass **8/8**, including:

- Standard pricing: **110.00**; rounding: **21.89**.
- Gold pricing: **104.50**; silver pricing: **107.80**.
- The order-total API tests return HTTP 200 for orders 1001, 1002, and 1003; the unknown-order test still returns 404.

I ran the suite before the fix and confirmed the reported **3 failures, 5 passes**. I have not applied the change, so the post-fix pass result remains to be verified by the implementer. The deployed localhost service is still 1.4.0; per scope, verification should use the tests, not the live API.

## Risks

Unlisted tiers continue to receive no discount, which is the previous behavior. If an unlisted tier is intended to have a discount, it must be added to the table explicitly. The existing tests do not exercise null or arbitrary unknown tiers, but the replacement handles them with the same zero-discount fallback.