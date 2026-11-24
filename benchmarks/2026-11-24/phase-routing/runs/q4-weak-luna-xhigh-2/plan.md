## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table contains `gold` and `silver`, but not `standard`; therefore calculating order 1001’s total throws `KeyNotFoundException`. The existing tests confirm this at that line.

The investigation finding about problem details is not supported: `Program.cs:5` registers `AddProblemDetails()`, and the `NotFound` helper is used for missing orders, not for known order 1001.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is { } tier ? TierDiscounts.GetValueOrDefault(tier) : 0m;
```

## Why this is the smallest safe fix

It is a single-line replacement in one file. Gold and silver continue to use their configured discounts; any unlisted tier—and a null tier—gets the zero discount that the 1.3.0 behavior gave non-gold customers. This fixes the regression without undoing the intended silver-tier discount or the 1.4.0 problem-details response for unknown orders.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests should pass, including the two standard-pricing tests and the order-total case for order 1001. The gold and silver pricing tests, totals for orders 1002 and 1003, and unknown-order 404 test should also remain passing.

Before the fix, `dotnet test` produced 3 failures and 5 passes. No fix was applied, so post-fix results are not yet available.

## Risks

An unlisted tier will silently receive no discount, as it did before the regression; a newly introduced tier must be added to the discount table to receive a discount. The patch will not change the deployed localhost service until redeployment; verification should be through `dotnet test`, not the live API.