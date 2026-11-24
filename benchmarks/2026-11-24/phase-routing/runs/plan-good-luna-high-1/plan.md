## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–10` defines discounts for `gold` and `silver`, but not `standard`. At `Pricing.cs:16`, `TierDiscounts[customer.Tier]` directly indexes the dictionary, so a standard-tier customer triggers `KeyNotFoundException`. `src/Orders.Api/OrderStore.cs:8,14` confirms order 1001 belongs to a standard-tier customer.

I reproduced the reported failures with `dotnet test`: **3 failed, 5 passed**, including the two pricing failures and the API test for order 1001.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry to the existing table:

**Before**
```csharp
static readonly Dictionary<string, decimal> TierDiscounts = new()
{
    ["gold"] = 0.05m,
    ["silver"] = 0.02m,
};
```

**After**
```csharp
static readonly Dictionary<string, decimal> TierDiscounts = new()
{
    ["gold"] = 0.05m,
    ["silver"] = 0.02m,
    ["standard"] = 0m,
};
```

Leave the lookup and the rest of the calculation unchanged.

## Why this is the smallest safe fix

It adds the missing valid tier without changing the pricing formula or the existing gold and silver discounts. Keeping direct lookup also avoids silently treating an unexpected or misspelled tier as a no-discount tier.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It should pass all **8 tests**, including standard pricing, rounding, and the API test that checks order 1001 returns HTTP 200. The existing API test also checks that orders 1002 and 1003 remain available.

## Risks

The fix assumes standard customers should receive no discount, consistent with the prior pricing behavior and existing standard-tier tests. Other unlisted tier values will still throw at the lookup.

I could not check `https://status.example.com`: the repository command tool denied `curl` and instructed me not to retry. No files were changed.