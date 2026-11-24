## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 only contains `gold` and `silver`. Order 1001 belongs to a `standard` customer (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException`. The endpoint passes that exception through the total calculation (`src/Orders.Api/Program.cs:23–24`).

The findings are supported by the code and commit `9bfbf7d`: it replaced the previous “gold gets 5%, otherwise zero” fallback with direct dictionary indexing. The current `dotnet test` run reproduces the reported three failures.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

This is a one-line change that keeps the lookup table and existing `gold` and `silver` discounts, while making a missing tier default to `0m`. That restores the prior behavior for standard customers and other unlisted tiers without changing the subtotal, tax, or rounding logic.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It must report **8 passed, 0 failed**, including the two standard-tier pricing tests and the API test for order 1001. Do not use the live API for verification: it is still the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

Unlisted or misspelled tiers will now receive no discount rather than throw; this matches the pre-commit fallback behavior but could conceal invalid tier data. The deployed service will remain unchanged until a redeploy. No files were modified.