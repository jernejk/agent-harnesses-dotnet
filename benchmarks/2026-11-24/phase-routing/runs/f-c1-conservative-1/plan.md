## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 contains `gold` and `silver`, but not `standard`, so pricing a standard-tier customer throws `KeyNotFoundException`.

This matches the incident: order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`), and the total endpoint calls `Pricing.Total` (`src/Orders.Api/Program.cs:24`). The cited commit introduced the direct lookup, replacing the prior rule that gave non-gold tiers a 0% discount. The existing pricing tests also expect standard customers to pay list price plus GST.

I ran `dotnet test`: **3 failed, 5 passed**. The two standard-tier pricing tests failed with the missing-key exception, and the order 1001 endpoint test returned 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add a 0% entry for the supported `standard` tier:

**Before:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It is a one-line change that restores the established standard-tier behavior while retaining the lookup table and both existing discounts. Adding an explicit supported tier is narrower than using a default for every missing key, which could hide unexpected or invalid tier values.

## Verification (what to run and what it must show)

Run `dotnet test`. It must pass all **8 tests**, including:

- Both standard-tier pricing tests, with expected totals of `110.00m` and `21.89m`.
- The endpoint test for order 1001, with HTTP 200.
- The existing gold and silver pricing tests.

Do not use the live API for verification: the local source change does not affect the deployed 1.4.0 service without a redeploy, which is out of scope.

## Risks

Unknown or misspelled tiers will still throw on dictionary lookup. That is intentional for this narrow fix: only the known `standard` tier is added. No files were changed.