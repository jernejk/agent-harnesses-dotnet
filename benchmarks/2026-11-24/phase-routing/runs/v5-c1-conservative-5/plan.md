## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The dictionary has entries for `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`.

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

It changes only the failing lookup. Known tiers retain their 1.4.0 discounts (`gold` and `silver`); any tier absent from the table receives the decimal default of `0m`. That restores the 1.3.0 behavior for `standard` and all other non-gold tiers without removing the intended silver discount.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should pass all 8 tests, including the standard-customer pricing and order-total API tests that currently fail. No new tests or live API verification are part of this plan.

## Risks

The fallback also gives zero discount for unlisted or misspelled tiers, matching the old non-gold behavior. The deployed localhost service remains on 1.4.0 until redeployed; this repository change alone will not alter its responses.