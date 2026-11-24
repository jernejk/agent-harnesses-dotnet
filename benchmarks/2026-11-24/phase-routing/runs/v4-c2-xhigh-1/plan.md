## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 contains only `gold` and `silver`, so `standard`—and any other absent tier—throws `KeyNotFoundException`. `Program.cs` passes the order’s customer to this pricing method, causing the standard-tier total request to return HTTP 500.

The history confirms the regression: in `v1.3.0`, pricing gave gold a 5% discount and every other tier a 0% discount. The 1.4.0 addition of silver at 2% is intentional; the direct index removed the fallback.

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

It changes one line in one file and retains the discount table: gold remains at 5%, silver at 2%, and any other tier defaults to 0%, matching the last good release’s fallback behavior. The null guard also preserves the old conditional’s safe 0% result for a null tier instead of passing null to the dictionary lookup.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`—not the live API. The current, unmodified suite reproduces **3 failed, 5 passed**. After the replacement, the existing suite should show **8 passed, 0 failed**, including standard-tier pricing and rounding, gold and silver pricing, all three order-total API cases (including order 1001), and unknown-order handling.

## Risks

Unknown or misspelled tiers will now receive no discount rather than raising an exception. That matches the previous release’s behavior, but can conceal bad tier data. The existing tests do not directly cover arbitrary unknown or null tiers. The deployed localhost service remains on 1.4.0 until redeployed; this plan does not change or verify that live deployment. No fix was applied.