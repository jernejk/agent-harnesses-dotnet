## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table at lines 7–11 includes only `gold` and `silver`, but order 1001 belongs to a `standard` customer (`src/Orders.Api/OrderStore.cs`). The lookup throws `KeyNotFoundException`, producing the 500.

This is introduced by commit `9bfbf7d` (“Move tier discounts into a lookup table and add silver tier”), not the cited `ab38e53`: the current `src/Orders.Api/Program.cs` registers `AddProblemDetails()`. The test run reproduced three failures, including the order-total test and two standard-tier pricing tests.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the existing standard tier.

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
        ["standard"] = 0.00m,
    };
```

No change has been applied.

## Why this is the smallest safe fix

It adds the known, supported tier that the data and tests already use, while preserving the existing direct lookup and the intended behavior: standard customers pay list price plus GST. It avoids broader fallback behavior that could silently treat an unexpected or misspelled tier as valid.

## Verification (what to run and what it must show)

- Run `dotnet test`: all 8 tests should pass, including the standard-tier pricing tests and `OrdersApiTests.Order_totals_are_available(orderId: 1001)`.
- Smoke-test `GET /orders/1001/total`: it should return HTTP 200 with a total of `195.80 AUD`.
- Smoke-test `GET /orders/9999/total`: it should return HTTP 404.

## Risks

An unknown or misspelled tier will still throw at the dictionary lookup; this plan deliberately avoids broadening behavior beyond the known `standard` tier. I could not check `https://status.example.com`: the repository command policy denied the request, and I did not retry it.