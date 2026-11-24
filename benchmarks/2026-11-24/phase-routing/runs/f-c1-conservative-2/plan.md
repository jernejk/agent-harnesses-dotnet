## Root cause (file:line)

The reported cause is supported by the code. `src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`. Order 1001 maps to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so pricing throws `KeyNotFoundException`.

The history confirms that commit `9bfbf7d` replaced the previous “gold gets 5%, everyone else gets 0%” logic with the strict dictionary lookup. The existing tests also explicitly expect standard-tier customers to receive no discount.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier with a zero discount.

**Before:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It adds the missing, known tier without changing the pricing formula or gold/silver discounts. Keeping the direct lookup also avoids silently treating arbitrary, unexpected tier values as standard.

## Verification (what to run and what it must show)

Run `dotnet test`. The current baseline is **3 failed, 5 passed**; the failures are both standard-tier pricing tests and the API total test for order 1001. After the change, the suite should show **8 passed, 0 failed**, including those three tests and the successful total requests for orders 1001, 1002, and 1003.

Do not use the live API for verification: the deployed 1.4.0 instance will not receive this repository change without redeployment, which is out of scope. The test suite exercises the API behavior locally.

## Risks

The direct lookup will still throw for tier strings other than `gold`, `silver`, and `standard`. That retains strict handling of unexpected tiers; confirm that those are the intended supported values. No source files were changed during this investigation.