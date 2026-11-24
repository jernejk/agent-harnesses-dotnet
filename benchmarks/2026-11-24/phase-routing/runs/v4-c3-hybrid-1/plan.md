## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes the discount table directly. The table has no `standard` entry, so order 1001 throws `KeyNotFoundException`.

The regression is broader than `standard`: 1.3.0 gave **every non-gold tier** a zero discount, including a null tier at runtime. The lookup introduced in `9bfbf7d` instead throws for missing keys and null.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`, make this one replacement:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

The fallback restores the old zero discount for unknown and null tiers. It retains the 1.4.0 lookup table and its intentional silver discount, as well as the gold discount. Adding only a `standard` entry would leave other missing and null tiers broken.

No code was changed for this plan.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` from the repo root. All **8 tests should pass**, including:

- Standard pricing and rounding; gold at 5% and silver at 2%.
- HTTP 200 for orders 1001, 1002 and 1003; HTTP 404 for an unknown order.

The current, unmodified build produced **3 failures and 5 passes**.

## Risks

- Existing tests do not cover arbitrary unknown or null tiers; their restored behavior follows from the replacement but is not independently tested by the suite.
- Tests exercise the repo build, not the deployed localhost service. The fix will not reach that service without a redeploy, which is out of scope.