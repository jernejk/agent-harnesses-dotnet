## Root cause (file:line)

The findings are supported by the code. `src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 has entries only for `gold` and `silver`. In `src/Orders.Api/OrderStore.cs:8,14`, order 1001 maps to customer 2, whose tier is `standard`. The missing key throws `KeyNotFoundException`; `src/Orders.Api/Program.cs:23–24` calls `Pricing.Total` for the total endpoint, resulting in HTTP 500.

The commit diff confirms this replaced the prior “gold gets 5%; otherwise zero” behavior. The current test run also reproduces the reported failure pattern.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry to the existing table:

**Before (lines 7–11):**
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

It adds the missing mapping for a tier already present in the order data and covered by pricing tests. It preserves the existing discount calculations and keeps the lookup’s fail-fast behavior for unrecognized tier values. No endpoint, calculation, or test changes are needed.

## Verification (what to run and what it must show)

After implementing the change, run `dotnet test` from the repository root. It must exit successfully with **8 passed, 0 failed**, including the standard-tier pricing tests and the API test for order 1001.

I ran `dotnet test` before any change: it exited with 3 failures and 5 passes, matching the reported failures. Do not use the live API for verification: the localhost service is still the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

Unrecognized tier strings will still throw if they are absent from the table. This fix covers the currently defined `gold`, `silver`, and `standard` tiers; restoring a general “all non-gold tiers get zero discount” fallback would be a broader behavior change. The deployed 1.4.0 service will remain affected until a separate redeployment.