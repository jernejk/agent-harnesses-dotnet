## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 contains only `"gold"` and `"silver"`, so order 1001’s customer, whose tier is `"standard"`, causes a `KeyNotFoundException` and an HTTP 500.

The regression is confirmed by the 1.3.0-to-1.4.0 diff and blame. The old code gave 5% off to gold customers and zero discount to every other tier. The current baseline `dotnet test` reproduces the reported results: **3 failed, 5 passed**.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is one find-and-replace in one file. It keeps the 1.4.0 lookup table and its new 2% silver discount, while restoring zero discount for standard and any other tier without a table entry. The null guard also avoids introducing an exception for a runtime null tier; the old comparison treated null as “not gold.”

## Verification

After the change, run `dotnet test`. The existing suite should show **8 passed, 0 failed**, including successful standard-tier pricing and order 1001, the gold and silver pricing expectations, order totals for 1001–1003, and the unknown-order 404 test. No new tests are needed. The current baseline is 3 failed, 5 passed; I did not apply the fix or verify a post-fix run.

## Risks

Unlisted tiers will continue to receive no discount, matching 1.3.0 behavior; they will not raise an error. The existing tests do not cover arbitrary or null tiers, although the proposed fallback handles them. The deployed 1.4.0 service will remain unchanged until redeployment, which is out of scope.