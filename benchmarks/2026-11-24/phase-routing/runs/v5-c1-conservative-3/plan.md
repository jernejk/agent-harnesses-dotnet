## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table contains `gold` and `silver`, but not `standard`, so that lookup throws `KeyNotFoundException` and makes the endpoint return HTTP 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

The default value for a missing decimal dictionary entry is zero, restoring the prior no-discount fallback for `standard` and any other tier absent from the lookup. Existing `gold` and `silver` entries still provide their intended discounts. This is a one-line change in one file.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`; all 8 existing tests must pass. In particular, standard-customer pricing and rounding tests should pass, and the order-total endpoint checks for order 1001 as well as the gold and silver orders should return success.

## Risks

Unknown or newly introduced tiers will receive no discount rather than fail, matching the prior fallback behavior. Case sensitivity and the configured gold/silver discount values remain unchanged. The current test run had 3 failures and 5 passes; I did not apply the fix or test the live API.