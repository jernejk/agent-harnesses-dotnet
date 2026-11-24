## Root cause (file:line)

The diagnosis is supported by the current code. `src/Orders.Api/Pricing.cs:7–11` defines discounts for `gold` and `silver` but omits `standard`; line 16 indexes the dictionary by `customer.Tier`, so a standard-tier customer triggers `KeyNotFoundException`.

Order 1001 maps to customer 2, who is standard-tier (`src/Orders.Api/OrderStore.cs:8,14`). The total endpoint passes that customer to `Pricing.Total` (`src/Orders.Api/Program.cs:23–24`). Blame attributes the dictionary and indexed lookup to commit `9bfbf7d8`; its parent used a 5% gold discount and zero for other tiers.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the existing `standard` tier.

**Before:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It adds the missing mapping for a tier already present in the customer data and required by the pricing tests. It preserves the existing gold and silver rates, GST calculation, and rounding without changing endpoint behavior or broadening the patch.

## Verification (what to run and what it must show)

Run `dotnet test` from the repository root. The current run reports 3 failed and 5 passed, including the two standard-pricing tests and the API test for order 1001. After the change, the suite should report **8 passed, 0 failed**; the standard pricing tests and all three order-total API cases should pass.

Do not use the live API for verification: it is still the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

The lookup will still throw for any tier string not explicitly listed. The proposed change covers the currently defined tiers; if unknown tier values should retain the old zero-discount behavior rather than fail fast, that policy needs a separate decision.