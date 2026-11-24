## Root cause (file:line)

Confirmed. `src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly using `customer.Tier`, but the table at lines 7–11 has entries only for `gold` and `silver`. Order 1001 is assigned customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`); `src/Orders.Api/Program.cs:23–24` passes that customer to `Pricing.Total`. The lookup therefore throws `KeyNotFoundException`.

The cited commit introduced this lookup, replacing the prior zero-discount behavior for non-gold tiers. The existing tests exercise standard-tier pricing and the order 1001 API total.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier to the discount table.

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

Leave the direct lookup at line 16 unchanged.

## Why this is the smallest safe fix

It adds the missing zero-discount value for the known `standard` tier, preserving the existing gold and silver rates and the pricing formula. It also keeps the current behavior for unrecognized tiers rather than silently treating them as standard.

## Verification (what to run and what it must show)

Run `dotnet test` after the change. It should report **8 passed, 0 failed**; in particular, the standard pricing and rounding tests should pass, and the API total tests should return `OK` for orders 1001, 1002, and 1003. The unknown-order test should continue to return 404.

I ran `dotnet test` against the current source: it reproduced **3 failures and 5 passes**, including the reported `KeyNotFoundException` and the 500 for order 1001. Use the test suite—not the live API—for verification; the localhost service remains on deployed 1.4.0 until redeployed.

## Risks

A tier missing from the table (including one with different casing) will still throw and may cause a 500. Add an explicit entry for each supported tier. No source files were changed during this investigation.