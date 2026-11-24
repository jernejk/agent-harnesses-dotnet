## Root cause (file:line)

The findings are supported. `src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts`, but the table at lines 7–11 contains only `gold` and `silver`. `OrderStore.cs:8` defines customer 2 as `standard`, and line 14 links order 1001 to that customer; `Program.cs:24` passes the customer to `Pricing.Total`.

Commit `9bfbf7d` introduced the lookup and replaced the previous “gold gets 5%, otherwise 0%” behavior. The current `dotnet test` run reproduces the reported **3 failed, 5 passed**, including the `standard` `KeyNotFoundException`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the standard tier.

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

It adds one entry for an existing tier and preserves the previous zero-discount behavior for standard customers. It leaves the pricing formula, endpoint, and gold/silver rates unchanged. Existing tests already cover standard pricing and the order 1001 API path, so no test changes are needed.

## Verification (what to run and what it must show)

From the repository root, run:

```text
dotnet test
```

The current run shows **3 failed, 5 passed**. After the change, the suite should show **8 passed, 0 failed**. In particular, the standard pricing and rounding tests should pass, and the API total test for order 1001 should return `200 OK`; existing gold, silver, and unknown-order tests should remain passing.

## Risks

- Unrecognized or newly introduced tiers will still throw at the dictionary lookup; legitimate new tiers must be added explicitly.
- The deployed 1.4.0 service will not receive this source change until redeployed. I made no code changes; verification should use `dotnet test`, not the live API.