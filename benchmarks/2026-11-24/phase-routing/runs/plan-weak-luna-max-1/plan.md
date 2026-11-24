## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–16`: the tier-discount dictionary contains `gold` and `silver`, but not `standard`. Line 16 indexes it using the customer’s tier, so `standard` throws `KeyNotFoundException`.

`src/Orders.Api/OrderStore.cs:8,14` maps order `1001` to a `standard` customer. The regression was introduced in `9bfbf7d`, which replaced the previous “gold gets 5%, otherwise 0%” logic with the dictionary lookup. The test inventory matches the reported failures: two standard-tier pricing tests and the API test for order 1001.

The reported cause in `Program.cs:30` is not supported by the current code: `AddProblemDetails()` is registered at line 5, and the not-found helper is only used when the order is missing. Order 1001 proceeds to `Pricing.Total`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the existing `standard` tier.

**Before:**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After:**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

Leave the lookup on line 16 unchanged.

## Why this is the smallest safe fix

One dictionary entry restores the prior behavior for standard customers while preserving the intended gold and silver discounts. It avoids changing endpoint handling or making unrecognized tiers silently receive a zero discount.

## Verification (what to run and what it must show)

After applying the change, run `dotnet test`. It should report **8 passed, 0 failed**, including the standard-tier pricing tests and `Order_totals_are_available(orderId: 1001)`. The existing gold, silver, rounding, and unknown-order tests should continue to pass.

Also verify `GET /orders/1001/total` returns **200** with a total of **195.8 AUD**; `GET /orders/9999/total` should remain **404**.

## Risks

An unrecognized tier will still throw; this fix deliberately handles the known `standard` tier without defining a fallback policy for unknown values. I could not verify `https://status.example.com`: the available tools did not provide an HTTP fetch, and the attempted `curl` request was denied. No repository files were changed, and I did not run the tests.