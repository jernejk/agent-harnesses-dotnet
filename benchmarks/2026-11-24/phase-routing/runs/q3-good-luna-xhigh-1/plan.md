## Root cause (file:line)

The findings are supported. In `src/Orders.Api/Pricing.cs:16`, `TierDiscounts[customer.Tier]` throws when a tier is absent from the dictionary. The table at lines 9–10 contains `gold` and `silver`, but not `standard`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`).

Commit `9bfbf7d` replaced the previous “gold gets 5%; other tiers get 0%” behavior with the direct lookup. Running `dotnet test` confirmed the reported baseline: **3 failed, 5 passed**, including both standard-tier pricing failures and the API test for order 1001.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`, replace:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

This is a one-line change that keeps the explicit gold and silver discounts while returning the default decimal value (`0`) for standard tiers. It restores the prior behavior for unlisted tiers without changing the API route or the pricing formula.

## Verification (what to run and what it must show)

After implementing the change, run `dotnet test` from the repository root. It should report **8 passed, 0 failed**, including the standard-tier pricing and rounding tests, and the API test for order 1001.

Do not use the live API to verify this patch: localhost is still running the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

An unlisted or misspelled tier will also receive no discount rather than raising an exception. That matches the previous fallback behavior; stricter tier validation would be a separate change. No source files were modified during this investigation.