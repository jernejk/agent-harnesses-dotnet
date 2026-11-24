## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer’s tier, but the dictionary at lines 7–11 contains only `"gold"` and `"silver"`. Order 1001 belongs to a `"standard"` customer (`OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException`; the total endpoint returns HTTP 500.

The investigation finding about missing problem-details registration is incorrect: `Program.cs:5` already calls `builder.Services.AddProblemDetails()`. The named `ab38e53` commit added that registration. The missing `"standard"` entry was introduced by `9bfbf7d`, which replaced the previous zero-discount fallback with the dictionary lookup.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the supported standard tier:

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
        ["standard"] = 0m,
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

## Why this is the smallest safe fix

It adds the missing supported tier without changing the pricing formula, endpoint behavior, or discounts for gold and silver customers. An explicit entry also avoids silently treating an unrecognized tier as standard.

## Verification (what to run and what it must show)

Run `dotnet test` after the change. It must report **8 passed, 0 failed**, including the standard-customer pricing tests and `Order_totals_are_available(orderId: 1001)`.

Before the fix, I ran `dotnet test`: it reported **3 failed, 5 passed**. The failures were the two standard-tier pricing tests and the order 1001 API test; their errors trace to the missing dictionary key.

## Risks

This addresses the currently configured standard tier. A future customer tier missing from `TierDiscounts` will still throw, which is intentional with this explicit mapping unless a broader fallback policy is separately requested. Do not use the deployed localhost API as verification: it remains on 1.4.0 until redeployment, which is out of scope.