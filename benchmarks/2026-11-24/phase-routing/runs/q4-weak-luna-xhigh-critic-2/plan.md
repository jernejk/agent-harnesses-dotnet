## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. That table contains `"gold"` and `"silver"`, but not `"standard"`, so pricing a standard-tier customer throws `KeyNotFoundException`. Order `1001` belongs to a standard-tier customer, which explains its HTTP 500.

The regression came from the tier-lookup change in `9bfbf7d`, between v1.3.0 and v1.4.0. In v1.3.0, every tier other than `"gold"` received no discount. The reported ProblemDetails cause is not supported: `Program.cs:5` registers `AddProblemDetails()`, and the existing unknown-order test passes.

## Change

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is one find-and-replace in one file. It retains the configured gold and silver discounts, while restoring the v1.3.0 zero-discount behavior for standard and any other unlisted tier. The null guard also preserves that fallback if a null tier reaches the method. It does not undo the intended silver-tier change or alter the not-found response behavior.

## Verification

Run `dotnet test` after the change. The current run reports **3 failed, 5 passed**: two standard-tier pricing tests fail with `KeyNotFoundException`, and the API test for order `1001` receives 500 instead of 200. After the fix, the existing suite should report **8 passed, 0 failed**. In particular, the standard-tier pricing tests and order `1001` total should pass; gold, silver, and unknown-order tests should continue to pass.

## Risks

The existing tests cover standard, gold, silver, and an unknown order, but not arbitrary or null tier values. The proposed fallback handles those as zero discount, matching the prior behavior. No repository files were changed; verification should be with `dotnet test`, not the deployed localhost API, which remains on 1.4.0.