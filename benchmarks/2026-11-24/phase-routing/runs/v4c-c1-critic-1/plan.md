## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table at lines 7–11 contains only `"gold"` and `"silver"`, so `"standard"`—and any other unlisted tier—throws `KeyNotFoundException`.

The reported cause is supported by the code. `OrderStore.cs` assigns order 1001 to a `"standard"` customer, and `Program.cs` calls `Pricing.Total` for the total endpoint. The history confirms the regression: before commit `9bfbf7d`, any tier other than exact `"gold"` received a zero discount. The silver discount added in 1.4.0 is intentional and should remain.

## Change (the exact lines before and after)

In **`src/Orders.Api/Pricing.cs`**, replace this one line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

This retains the existing `"gold"` and `"silver"` lookup values and returns zero for missing tiers, as well as for null.

## Why this is the smallest safe fix

It is a one-line, one-file change that restores the previous zero-discount fallback for every unlisted tier—not just `"standard"`—while preserving the intended silver discount. It also preserves the old behavior for a null tier. The subtotal, GST, and rounding calculations remain untouched.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`; the existing suite should pass **8/8 tests**, including standard-tier pricing and rounding, gold and silver pricing, all three order-total endpoint cases, and the unknown-order 404 case.

I ran `dotnet test` before any change: **3 failed and 5 passed**, matching the reported `KeyNotFoundException` and the order 1001 endpoint failure. I did not apply the fix, so the post-fix result remains to be verified by the implementer. Use the test suite, not the localhost API: the deployed 1.4.0 build will not include this repo change without a redeploy.

## Risks

Unlisted tiers will continue to receive zero discount, matching the prior behavior. If a future tier should receive a discount, it must be added to `TierDiscounts`. The working tree remains unchanged.