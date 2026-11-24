## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly, but the table at lines 7–11 contains only `"gold"` and `"silver"`. The 1.3.0 implementation gave a discount only to exact `"gold"`; every other tier got zero. Thus `"standard"` now throws `KeyNotFoundException`.

The data and route confirm the reported failure: order `1001` belongs to customer `2`, whose tier is `"standard"` (`OrderStore.cs:8, 14`), and the total route calls `Pricing.Total` (`Program.cs:23–24`). The current test run reproduces the reported three failures: two pricing tests and the API test for order `1001`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It is one line in one file and keeps the 1.4.0 lookup table and its intended 2% silver discount. Known tiers retain their configured discounts; any unlisted tier gets zero, matching 1.3.0 behavior. The null guard also preserves the old comparison’s behavior for a runtime null tier without passing null to the dictionary.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All **8 existing tests must pass**. In particular, the standard-tier pricing tests and the API test for order `1001` must pass; the gold and silver pricing/API tests must continue to pass. The current baseline is 3 failed and 5 passed. No new tests or live API checks are needed.

## Risks

Unlisted tiers will continue to receive zero discount, matching the last good release; if a future tier should receive a discount, it must be added to the lookup table. The running localhost service is still the deployed 1.4.0 build: this repository change will not affect it until redeployment, which is out of scope. No files were changed.