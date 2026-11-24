## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly, but the table at lines 7–11 contains only `gold` and `silver`. Order 1001 belongs to a `standard` customer (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException`.

The history supports the finding: commit `9bfbf7d` replaced the prior gold-or-zero fallback with the direct lookup. The current `dotnet test` run reproduces the three reported failures: **3 failed, 5 passed**.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It is a one-line change that uses configured discounts for `gold` and `silver`, while returning zero for tiers not in the table—preserving the previous fallback behavior without changing the total, GST, or rounding calculations.

## Verification (what to run and what it must show)

Run `dotnet test` from the repository root. It must pass all **8 tests**, including standard-tier pricing and rounding, silver pricing, and the API order-total checks for orders 1001–1003.

Do not use the live API for verification: it is still running deployed version 1.4.0, and a repository change will not affect it without redeployment.

## Risks

The fallback means an unknown or misspelled tier receives no discount rather than raising an error; that matches the prior behavior. The deployed service will remain unchanged until a redeploy, which is out of scope.