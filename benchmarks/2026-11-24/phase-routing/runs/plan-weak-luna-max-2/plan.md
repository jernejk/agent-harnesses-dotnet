## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 has entries only for `"gold"` and `"silver"`. Order `1001` belongs to a `"standard"` customer (`src/Orders.Api/OrderStore.cs`), so the lookup throws `KeyNotFoundException`; that exception causes the HTTP 500.

The test run confirms this: the endpoint failure and both standard-tier pricing failures point to `Pricing.Total` line 16. The supplied problem-details diagnosis is not supported by the code: `ab38e53` registers `AddProblemDetails()` in `Program.cs`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry:

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

It restores the existing `"standard"` tier behavior from 1.3.0—no discount—without changing the calculation, endpoint response path, or handling of unrecognized tiers. Existing tests already cover standard-tier pricing and the affected endpoint.

## Verification (what to run and what it must show)

- Run `dotnet test`. It must pass all 8 tests, including standard-tier pricing and `Order_totals_are_available(orderId: 1001)`.
- Smoke-test `GET /orders/1001/total`: it must return HTTP 200. With the current order data and pricing formula, the total should be 195.80 AUD.
- Confirm the unknown-order test still returns 404.

The unchanged repository currently reports 3 failed and 5 passed tests.

## Risks

- The lookup remains case-sensitive and will still throw for an unknown or misspelled tier; this preserves visibility of invalid tier data rather than silently assigning a discount.
- I could not verify `https://status.example.com`: the status-page request was blocked by the command policy, and no HTTP fetch tool is available here. I made no code changes.