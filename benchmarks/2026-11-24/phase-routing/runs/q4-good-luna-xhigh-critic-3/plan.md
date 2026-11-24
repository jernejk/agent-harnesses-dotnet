## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table contains `"gold"` and `"silver"` but not `"standard"`, so the lookup throws `KeyNotFoundException` for order 1001’s customer.

Commit `9bfbf7d` introduced this lookup, replacing the previous behavior: `"gold"` received 5% off and every other tier received no discount. The findings are supported by the code and the reproduced test failures.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this single line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

Keep the discount table and the rest of the method unchanged. The null guard also preserves the old behavior for a null tier, which the former gold comparison treated as zero discount.

## Why this is the smallest safe fix

This is a one-line replacement in one file. It retains the intentional 1.4.0 rates for gold and silver, while making unlisted tiers—including `"standard"`—receive the zero discount they received before 1.4.0. It also avoids a null-key lookup exception.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` against the patched repository—not the deployed localhost service. The existing suite should pass all 8 tests (`Failed: 0`), including:

- Standard pricing: `$110.00`; standard rounding: `$21.89`.
- Gold pricing: `$104.50`; silver pricing: `$107.80`.
- HTTP 200 for order totals 1001, 1002, and 1003; HTTP 404 for an unknown order.

On the unmodified checkout, `dotnet test` reproduced the reported result: 3 failed and 5 passed.

## Risks

Unknown tiers continue to receive zero discount, matching the previous release; any future tier needing a discount must be added to the table. Existing tests do not directly cover null or arbitrary unknown tiers. No fix was applied, and the live 1.4.0 service was not used for verification.