## Root cause (file:line)

The reported cause is supported by the code. In `src/Orders.Api/Pricing.cs:16`, `Pricing.Total` indexes `TierDiscounts` with `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`. Order 1001 belongs to the `standard` customer (`src/Orders.Api/OrderStore.cs:8,14`), so the index throws `KeyNotFoundException`. The total endpoint calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`, producing the 500.

Commit `9bfbf7d` introduced the direct lookup in place of the previous gold-or-zero-discount logic.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the supported `standard` tier.

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

It changes only the missing mapping and preserves the existing lookup behavior for gold and silver. It restores the intended zero-discount pricing for the known `standard` tier without making unexpected tier names silently default to zero. Existing tests already cover standard-tier pricing and the order-total endpoint, so no test changes are needed for this fix.

## Verification (what to run and what it must show)

Run:

```sh
dotnet test
```

The current run fails three tests: the two standard-tier pricing tests and the order 1001 endpoint test. After the change, the suite should pass all 8 tests. In particular, it must show standard pricing totals of `110.00` and `21.89`, and successful total responses for orders 1001, 1002, and 1003.

Do not use the live API as verification: the deployed 1.4.0 service will not receive this source change without a redeploy.

## Risks

Unsupported or misspelled tier names will still throw during lookup. This retains a visible failure for invalid tier data rather than treating it as a valid zero-discount tier.