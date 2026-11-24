## Root cause (file:line)

Confirmed. In `src/Orders.Api/Pricing.cs:16`, `Pricing.Total` indexes `TierDiscounts` directly. The table at lines 7–11 has entries for `gold` and `silver`, but not `standard`. Order 1001 maps to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException` and the route returns HTTP 500.

The cited commit introduced this regression: it replaced the prior zero-discount fallback for non-gold customers with the dictionary indexer.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`, replace:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
```

## Why this is the smallest safe fix

This is a one-line change. It preserves the existing gold and silver discounts and restores the previous behavior for tiers without an entry: no discount. Adding only a `standard` entry would fix the reported order but would leave other unlisted tiers capable of causing the same exception.

## Verification (what to run and what it must show)

After implementing the change, run:

```text
dotnet test
```

It must pass all 8 tests, including the standard-tier pricing tests and the API test for order 1001. The current run is a failing baseline: 3 failed, 5 passed; it reproduced both `standard` lookup exceptions and the order-1001 HTTP 500.

Do not use the localhost API as verification: it is still the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

Unlisted tiers will receive no discount rather than fail. That matches the prior behavior, but could conceal a misspelled or unexpectedly introduced tier. No files were changed.