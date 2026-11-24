## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–16`: commit `9bfbf7d` introduced a discount lookup containing `gold` and `silver`, but not `standard`. The lookup at line 16 throws `KeyNotFoundException` for standard customers. Order 1001 belongs to a standard customer (`OrderStore.cs:8,14`).

The reported problem-details cause is not supported: `Program.cs:5` already calls `AddProblemDetails()`, and the existing unknown-order 404 test passes. The current `dotnet test` run produced three failures: two standard-tier pricing tests and the order 1001 API test.

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

It restores the standard tier’s zero-discount behavior without changing gold or silver pricing, rounding, or the 404 response. Existing tests already cover standard pricing, rounding, all three stored orders’ response statuses, and unknown-order status.

## Verification (what to run and what it must show)

After implementing the change, run `dotnet test`. All **8 tests must pass**, including the standard-tier pricing and rounding tests, the order 1001 total endpoint test, and the 404 test. The endpoint test checks the status, not the response body; the pricing tests check the calculated amounts.

## Risks

Unrecognized tier names will still throw. That preserves the current fail-fast behavior rather than silently assigning a discount; handling invalid tiers is a separate decision. This plan was not applied, and the deployed 1.4.0 service will remain unchanged until redeployment.