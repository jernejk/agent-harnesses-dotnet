## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the table has no `"standard"` entry. Order `1001` belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`), so pricing throws `KeyNotFoundException`, which causes the totals endpoint to return HTTP 500.

The cause is supported by the code and history: commit `9bfbf7d` replaced the previous behavior—5% off for gold and zero for every other tier—with a lookup containing only gold and silver. The existing tests also expect standard customers to receive no discount.

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

This is a one-line change that restores the previous zero-discount behavior for tiers absent from the table, while retaining the configured gold and silver discounts. It also handles other unlisted tiers consistently with the prior implementation, rather than adding only the currently observed `"standard"` entry.

## Verification (what to run and what it must show)

Run:

```sh
dotnet test
```

It must pass all 8 tests, including standard-tier pricing and the API totals theory for order `1001`. Before the fix, the run reproduced 3 failures and 5 passes. Do not use the live API for verification: it is still running deployed version 1.4.0, and redeployment is out of scope.

## Risks

Unlisted tiers will continue to receive zero discount, matching the previous behavior; that may be inappropriate if a new tier is intended to have a specific discount. The lookup remains case-sensitive, as before. No source files were changed during this investigation.