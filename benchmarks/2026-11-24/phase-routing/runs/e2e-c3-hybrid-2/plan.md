## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts`, which has no `standard` entry. Order 1001 belongs to a standard customer (`OrderStore.cs:8,14`), so the total route throws and returns 500.

Commit `9bfbf7d` introduced the direct lookup in place of the previous zero-discount fallback. I reproduced the reported baseline: **3 of 8 tests fail**, including the order 1001 API test.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:7–11`:

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

It adds the missing mapping without changing discounts, rounding, or handling of other tiers. Existing tests already cover standard pricing, rounding, and the affected API route; no test changes are required for this incident.

## Verification (what to run and what it must show)

- Run `dotnet test`: all 8 tests must pass, including standard, gold, silver, rounding, and all three order-total API cases.
- Run the API with `dotnet run --project src/Orders.Api` and request `/orders/1001/total`: it must return HTTP 200 with order ID `1001`, total `195.80`, and currency `AUD`. Orders 1002 and 1003 must still return 200.

## Risks

Unlike 1.3.0, an unmapped tier still throws rather than receiving zero discount. This fix deliberately leaves that behavior unchanged; confirm separately whether arbitrary unknown tiers should regain the old fallback. No fix was applied.