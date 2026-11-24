## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with the customer’s tier. The table at lines 7–11 has `"gold"` and `"silver"` but no `"standard"` entry. Order `1001` belongs to a standard-tier customer, so its total request throws `KeyNotFoundException` and returns HTTP 500.

The supplied Problem Details cause is not supported: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`, and the Problem Details helper is only used for unknown orders. The regression was introduced in commit `9bfbf7d`, which replaced the previous “gold gets 5%; otherwise zero” behavior with the incomplete lookup table.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard-tier entry to the existing dictionary:

**Before**
```csharp
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After**
```csharp
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It adds one entry for a tier already used by the current order data and restores the prior zero-discount behavior for standard customers. It leaves the discount calculation and endpoint behavior unchanged.

## Verification (what to run and what it must show)

Run `dotnet test`. The current run reproduces 3 failures and 5 passes, including the `KeyNotFoundException` and HTTP 500 for order `1001`. After the change, the suite should report **8 passed, 0 failed**; in particular, the order-total test for `1001` and the standard-customer pricing tests should pass. The unknown-order test should continue to return 404.

Do not use the live localhost API for verification: it is still running the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

This fixes the known `"standard"` tier, but the dictionary indexer will still throw for any other unlisted tier. The current store uses only gold, silver, and standard; deciding how to handle future or invalid tiers is outside this minimal fix.