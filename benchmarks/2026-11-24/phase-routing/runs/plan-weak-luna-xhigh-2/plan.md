## Root cause (file:line)

`src/Orders.Api/Pricing.cs:10,16`: the tier-discount lookup has entries for `"gold"` and `"silver"` but not `"standard"`, and `Pricing.Total` indexes it directly. Order 1001 belongs to a `"standard"` customer, so the lookup throws `KeyNotFoundException` and the endpoint returns HTTP 500.

The omission was introduced by `9bfbf7d` (“Move tier discounts into a lookup table and add silver tier”), which replaced the previous zero-discount fallback. The cited `ab38e53` change is not the cause: `AddProblemDetails()` is registered in `Program.cs:5`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for standard customers:

**Before**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It restores the existing standard-customer pricing behavior without changing endpoint error handling, discount calculations, or supported gold and silver discounts. Existing tests already cover standard-customer pricing and the order-total endpoint.

## Verification (what to run and what it must show)

Run `dotnet test`. It should report **8 passed, 0 failed**, including the standard pricing tests and `OrdersApiTests.Order_totals_are_available(orderId: 1001)`. That endpoint test should receive HTTP 200 rather than 500.

## Risks

This only handles the `"standard"` tier already present in the order data; an unrecognized tier would still cause a lookup exception. The requested status-page check could not be completed: the environment denied the HTTP request and instructed me not to retry it. No code was changed.