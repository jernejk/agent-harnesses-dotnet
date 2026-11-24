## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly using the customer’s tier. The table contains `gold` and `silver`, but not `standard`, so a standard-tier request throws `KeyNotFoundException`.

The findings are confirmed: `src/Orders.Api/OrderStore.cs:8,14` associates order 1001 with a standard-tier customer. Commit `9bfbf7d` replaced the previous zero-discount fallback for non-gold tiers with the direct lookup.

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

It changes only the failing lookup and restores the previous behavior: known tiers use their configured discount, while tiers without an entry receive no discount. It leaves the silver discount, GST calculation, rounding, customer data, and API behavior for supported tiers unchanged.

## Verification (what to run and what it must show)

From the repository root, run:

```text
dotnet test
```

The current baseline reproduces the reported 3 failures and 5 passes. After the change, the full suite should pass: **8 passed, 0 failed**, including the standard pricing and rounding tests and the API test for order 1001.

Do not use the live API for verification: the deployed 1.4.0 service will not include this repository change without a redeploy, which is out of scope.

## Risks

An unlisted or misspelled tier will now receive no discount rather than throw. That matches the prior fallback behavior; any newly supported discounted tier must still be added to `TierDiscounts`.