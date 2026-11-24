### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The dictionary at lines 7–11 contains `gold` and `silver`, but not the established `standard` tier. Order 1001 uses customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException` and the endpoint returns HTTP 500.

The cited commit confirms the regression: it replaced the previous “gold gets 5%, otherwise 0%” expression with the dictionary lookup. `dotnet test` reproduced the three reported failures, including the stack trace at `Pricing.cs:16`; five other tests passed.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the known `standard` tier:

**Before**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

Leave the lookup and pricing calculation unchanged.

### Why this is the smallest safe fix

It adds the missing entry for a tier already used by the order data and covered by pricing tests. It restores the established zero-discount behavior for standard customers while preserving the silver and gold discounts, and avoids changing fallback behavior or pricing arithmetic.

### Verification (what to run and what it must show)

After implementing the change, run:

```text
dotnet test
```

It should pass all 8 tests, including both standard-pricing tests and the API test for order 1001. The existing tests also check that orders 1002 and 1003 remain available and an unknown order returns 404.

Do not use the live API as verification: the localhost service is still the deployed 1.4.0 build, and a repository change will not affect it without a redeploy.

### Risks

The dictionary remains a strict lookup: a future or unexpected tier without an entry will still throw. Add an explicit mapping and corresponding test when introducing another supported tier. No source files were changed during this investigation.