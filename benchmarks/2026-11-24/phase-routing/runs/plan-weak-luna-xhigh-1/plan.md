## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`: `Pricing.Total` indexes `TierDiscounts` using the customer tier, but the table at lines 7–11 has entries only for `"gold"` and `"silver"`. Order 1001 belongs to a `"standard"` customer, so the lookup throws `KeyNotFoundException`, producing the HTTP 500.

The `dotnet test` run reproduced this: three failures, including the 1001 API test and two standard-tier pricing tests. The supplied problem-details explanation does not match the code: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`, and the unknown-order test passes. The omitted standard-tier entry was introduced in commit `9bfbf7d`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier with no discount:

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
        ["standard"] = 0.00m,
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

## Why this is the smallest safe fix

It restores the existing zero-discount behavior for the known `"standard"` tier while preserving the current gold and silver discounts. It avoids changing the unrelated not-found response path or silently assigning a default discount to unknown tier names.

## Verification (what to run and what it must show)

Run `dotnet test`. After the change, it should show **8 passed, 0 failed**, including:

- Both standard-tier pricing tests.
- `OrdersApiTests.Order_totals_are_available(orderId: 1001)` passing with HTTP 200.
- `OrdersApiTests.Unknown_orders_return_404` still passing.

## Risks

Unknown or misspelled tier names will still throw at the dictionary lookup; the proposed change intentionally covers the known standard tier rather than broadening fallback behavior. I could not verify `https://status.example.com`: the available command policy blocked `curl`, and no HTTP fetch tool was exposed. No files were modified.