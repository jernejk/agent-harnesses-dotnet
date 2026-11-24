## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` by the customer’s tier, but the dictionary at lines 7–11 has entries only for `"gold"` and `"silver"`. Order `1001` belongs to a `"standard"` customer, so the lookup throws `KeyNotFoundException`; the API returns HTTP 500.

The omitted entry was introduced in `9bfbf7d`: before that change, every non-gold customer received a zero discount. The current test run reproduces the reported **3 failed, 5 passed** result, including the standard-tier pricing tests and `Order_totals_are_available(orderId: 1001)`.

The supplied claim about `NotFound` is contradicted by the code: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`, and the unknown-order 404 test passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier to the existing dictionary.

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

It restores the existing standard-customer behavior—no discount—while retaining the tier lookup and the gold and silver discounts. It avoids changing endpoint response handling, which is not the cause of the known-order 500.

## Verification (what to run and what it must show)

- Run `dotnet test`: all **8 tests pass**, with **0 failures**.
- Verify `GET /orders/1001/total` returns **200** and a total of **195.8 AUD**.
- Verify `GET /orders/9999/total` still returns **404**.

## Risks

An unrecognized tier will still cause a dictionary lookup exception. This change covers the currently supported tiers; deciding how to handle invalid or newly introduced tiers is outside this minimal fix.

I could not check `https://status.example.com`: the available command tool blocked the `curl` request, and no HTTP-fetch tool was available. No repository files were changed.