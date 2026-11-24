## Root cause (file:line)

The findings are supported by the code. In `src/Orders.Api/Pricing.cs:7–11`, the discount table contains only `gold` and `silver`; line 16 indexes it directly with `customer.Tier`. `src/Orders.Api/OrderStore.cs:8,14` identifies order 1001’s customer as `standard`, so that lookup throws `KeyNotFoundException`. The `/total` handler calls pricing at `src/Orders.Api/Program.cs:23–24`.

The cited commit introduced the regression: it replaced the previous gold-or-zero fallback with the direct dictionary lookup. The test run reproduced the issue: **3 failed, 5 passed**, including the API test for order 1001.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry for the supported `standard` tier.

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

It adds one entry for a tier already present in the customer data and covered by existing tests. It preserves the table-driven pricing and existing gold and silver rates, while retaining the direct lookup so an unexpected tier does not silently receive a zero discount.

## Verification (what to run and what it must show)

Run `dotnet test` from the repo root after the change. It must show **8 passed, 0 failed**. In particular, the standard pricing and rounding tests must pass, and the API test for order 1001 must receive `OK`; the gold and silver pricing tests must also remain passing.

The current pre-change run has 3 failures and 5 passes. No files were changed, and the working tree remains clean. Do not use the live API as verification: the deployed 1.4.0 instance will not receive this repo patch without a redeploy.

## Risks

The deployed 1.4.0 service will continue to exhibit the issue until redeployed; deployment is outside this plan. The fix handles the known `standard` tier, but any other unlisted tier will still throw at the dictionary lookup.