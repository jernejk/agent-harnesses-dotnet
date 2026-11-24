## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` by customer tier. The dictionary contains only `gold` and `silver`, so an unlisted tier such as `standard` throws `KeyNotFoundException`. Confirmed: `dotnet test` failed 3 of 8 tests, including the API test for order 1001.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier ?? string.Empty);
```

## Why this is the smallest safe fix

This is a single-line replacement in one file. It keeps the configured `gold` and `silver` discounts, while unlisted tiers—including a null tier—receive the zero-discount fallback. That restores the prior non-gold fallback without losing the intended silver-tier change in 1.4.0.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite must pass all 8 tests, including the standard-tier pricing tests and the API test for order 1001. Do not add tests.

## Risks

Unlisted tiers will continue to receive no discount, matching the prior fallback; a new tier must be added to the lookup to receive a discount. The local deployed 1.4.0 service will remain unchanged until redeployment, which is out of scope.