## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table (lines 7–11) contains `gold` and `silver`, but not `standard`; any tier absent from the table throws `KeyNotFoundException`. The one test run confirmed the reported three failures and their causes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

This is a one-line, one-file replacement. It retains the configured discounts for gold and silver and uses zero for tiers not in the table, restoring the prior fallback behavior for standard and other unlisted tiers while keeping the intended silver-tier change.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`; all 8 existing tests should pass, including the two pricing tests and the API test for order 1001 that failed before the fix. No live-API check is needed: the running service remains on 1.4.0, and redeployment is out of scope.

## Risks

Unlisted tiers continue to receive no discount, as under the prior fallback behavior. Gold and silver discounts are unchanged. The local patch will not affect the deployed service until redeployment.