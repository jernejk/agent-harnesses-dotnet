## Root cause (file:line)

Confirmed: `src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The dictionary at lines 7–11 contains only `gold` and `silver`, so the `standard` tier throws `KeyNotFoundException`.

This matches the cited commit: it replaced the previous “gold gets 5%, all other tiers get 0%” conditional with direct dictionary lookup. The total route calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`; order 1001 uses customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`).

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, insert one entry after the silver entry:

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

## Why this is the smallest safe fix

It restores the expected zero-discount behavior for the known `standard` tier with a one-line change. It leaves gold and silver pricing unchanged and preserves the current fail-fast behavior for unrecognized tier names, rather than silently treating bad tier data as standard.

## Verification (what to run and what it must show)

Run `dotnet test` from the repository root. The current baseline is 3 failed and 5 passed; after the change, it should show **8 passed, 0 failed**, including both standard-tier pricing tests and `Order_totals_are_available(orderId: 1001)`.

No live API check is needed: the deployed 1.4.0 service will not receive a repository patch without a redeploy, which is out of scope.

## Risks

An unrecognized or misspelled tier will still cause a lookup failure and potentially a 500. That is intentional in this narrow fix; any newly supported tier should be added explicitly. No source files were changed.