## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the dictionary at lines 7–11 has entries only for `gold` and `silver`. Order 1001 belongs to customer 2 (`src/Orders.Api/OrderStore.cs:8,14`), whose tier is `standard`. The missing key throws `KeyNotFoundException`; the total endpoint calls this pricing method.

This is confirmed by the prior implementation: non-gold customers received a zero discount. Commit `9bfbf7d` replaced that behavior with the dictionary lookup but omitted the `standard` entry. Running `dotnet test` reproduced the reported 3 failures and 5 passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the supported standard tier to the dictionary:

**Before**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
```

**After**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
```

## Why this is the smallest safe fix

It restores the existing zero-discount behavior for standard customers while retaining the intended gold and silver discounts and leaving the calculation unchanged. It also keeps the dictionary’s lookup behavior explicit: unconfigured tier names will still fail rather than silently receiving an assumed discount.

## Verification (what to run and what it must show)

Run `dotnet test`. It should report all 8 tests passing, including the standard-tier pricing tests and the API test for order 1001. The API test should show that order 1001 returns HTTP 200.

## Risks

Unrecognized tier names will still throw `KeyNotFoundException`. If such names should be accepted with no discount, that fallback policy should be decided separately rather than added as part of this targeted fix.

No code was written or applied; the working tree was clean.