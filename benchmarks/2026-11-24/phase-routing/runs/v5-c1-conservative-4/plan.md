## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The dictionary at lines 7–11 has entries for `gold` and `silver`, but not `standard`. The single `dotnet test` run reproduced the `KeyNotFoundException` and the API test’s HTTP 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

This single-line replacement restores zero discount for tiers without a dictionary entry, matching the prior non-gold fallback, while retaining the 1.4.0 discounts for gold and silver. It changes no other pricing logic.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests should pass, including the two pricing tests and the API total test for order 1001. Do not add tests or use the live API for verification.

## Risks

Unknown or misspelled tiers will receive zero discount rather than causing a lookup exception; that is consistent with the prior behavior. The local service is still the deployed 1.4.0 build, so this repository change will not affect it without a redeploy, which is out of scope.