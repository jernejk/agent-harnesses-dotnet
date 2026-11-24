## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table has entries for `gold` and `silver`, but not `standard`, so standard-tier orders throw `KeyNotFoundException`. The single `dotnet test` run confirmed the reported three failures; five tests passed.

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

A missing tier now gets the dictionary’s default `decimal` value, zero—restoring the previous no-discount behavior for standard and any other unlisted tiers. Listed tiers still use their configured discounts, including the intended 2% silver discount. This is one replacement in one file and leaves the remaining price calculation unchanged.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests must pass, including the standard-tier pricing and rounding tests and the API order-total test for order 1001.

## Risks

Unlisted or misspelled tier names will continue to receive zero discount rather than raising an error. That matches the prior fallback behavior, but could make future tier-data mistakes less visible.