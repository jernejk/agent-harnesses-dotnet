## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` by `customer.Tier`, but the dictionary at lines 7–11 contains only `"gold"` and `"silver"`. Order 1001 maps to customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`), so pricing throws `KeyNotFoundException`. The `/orders/{id}/total` handler calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`.

This matches the regression introduced by `9bfbf7d`: it replaced the previous non-gold zero-discount fallback with the dictionary lookup. The `ab38e53` change concerns unknown-order handling and does not explain this failure.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry for standard-tier customers.

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

It adds one entry for a tier that is present in the order data and already has zero-discount expectations in the pricing tests. It preserves the explicit lookup, discount rates for gold and silver, and current behavior for unrecognized tiers rather than broadening the fallback.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

The current run fails 3 of 8 tests: two standard-tier pricing tests throw `KeyNotFoundException`, and the API test for order 1001 returns `InternalServerError`. After the change, the suite should pass all 8 tests, including the standard pricing expectations and the API test requiring order 1001’s total to return `OK`. Do not use localhost API behavior as verification: it is still the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

The lookup will still throw for tiers other than `"gold"`, `"silver"`, and `"standard"`. That retains the current explicit failure behavior for unrecognized tier values. The deployed service will continue to exhibit the incident until a new build is deployed.

No files were changed.