## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`. Commit `9bfbf7d` replaced the prior rule—gold customers get 5% off; all other tiers get no discount—with a direct dictionary index. Since `standard` is not in the dictionary, `Pricing.Total` throws `KeyNotFoundException`. Order `1001` belongs to a standard customer, so this causes its HTTP 500.

The reported problem-details cause does not match the code: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`. The test stack trace points to `Pricing.Total`, and the existing unknown-order test is consistent with the 404 path working.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It is one line in one file. Gold and silver still receive their 1.4.0 discounts; missing tiers (including `standard`) default to no discount, restoring the prior behavior for unlisted tiers. The null guard also preserves the previous behavior if a null tier reaches this method. This leaves the 1.4.0 problem-details behavior unchanged.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should pass all 8 tests, including standard pricing and rounding, gold and silver discounts, successful totals for orders `1001`, `1002`, and `1003`, and a 404 for an unknown order. Do not add tests or verify against the live API.

## Risks

Unrecognized tiers will continue to receive no discount rather than throw, matching the prior behavior; that can hide a misspelled tier. The running localhost service remains on deployed 1.4.0 until redeployed, which is out of scope. No files were changed.