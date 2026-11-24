## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` by customer tier, but `src/Orders.Api/Pricing.cs:7–11` contains entries only for `"gold"` and `"silver"`. Order 1001 is assigned to customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`); therefore the lookup throws `KeyNotFoundException`. The `/orders/{id}/total` endpoint calls `Pricing.Total` (`src/Orders.Api/Program.cs:24`), which turns this into HTTP 500.

The cited commit is the cause: it replaced the prior gold-versus-zero-discount logic with direct dictionary indexing but did not add the existing `"standard"` tier.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry to the dictionary:

**Before**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
```

**After**
```csharp
        ["gold"] = 0.05m,
        ["standard"] = 0m,
        ["silver"] = 0.02m,
```

## Why this is the smallest safe fix

It adds the missing known tier with the no-discount behavior already expected for standard customers, without changing the pricing formula, endpoint, or gold and silver discounts. Existing tests explicitly expect standard-tier pricing to work.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

Before the fix, this run reproduced 3 failures and 5 passes: the standard pricing tests throw `KeyNotFoundException`, and the endpoint test for order 1001 returns 500. After the fix, the suite should pass all 8 tests, including standard pricing and the endpoint test for order 1001.

Do not use the live localhost API as verification: it is still running the deployed 1.4.0 build, and this repository change will not reach it without a redeploy.

## Risks

This explicitly handles the known `"standard"` tier. Any other unlisted tier would still throw on dictionary lookup; decide separately whether unknown tiers should be rejected or receive a fallback discount. No source changes were made.