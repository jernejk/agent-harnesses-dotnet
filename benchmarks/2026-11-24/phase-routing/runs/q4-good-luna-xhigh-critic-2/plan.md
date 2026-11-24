## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table contains only `"gold"` and `"silver"`, so a standard tier (or any other absent key) throws `KeyNotFoundException`.

The report is supported by the code and history: commit `9bfbf7d` replaced the prior “gold gets 5%, everything else gets 0%” fallback with the direct lookup. The existing `dotnet test` run reproduces the reported three failures: two pricing tests throw for `"standard"`, and the API test for order 1001 fails.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16 only:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

It is one line in one file. It keeps the lookup table and honors the intended `"gold"` and newly added `"silver"` discounts, while restoring a zero discount for every other tier, as the previous release did. The null guard also preserves the old fallback for a null tier; calling `TryGetValue` with a null key would otherwise still throw. The subtotal, GST, and rounding behavior remain unchanged.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests should pass, including both standard-tier pricing tests and the API test for order 1001. The current baseline is 3 failed and 5 passed. Do not use the localhost API for verification: it is still the deployed 1.4.0 build.

## Risks

The existing tests cover standard, gold, and silver tiers but not arbitrary unknown or null tiers. The replacement restores their prior zero-discount behavior, but that behavior will not have direct test coverage under the instruction not to add tests. A zero discount for an unrecognized tier may also conceal invalid tier data, but it matches the previous release’s behavior.