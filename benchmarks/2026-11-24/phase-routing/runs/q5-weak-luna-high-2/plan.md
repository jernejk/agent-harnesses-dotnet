## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`. Commit `9bfbf7d` replaced the previous “gold gets a discount, all other tiers get zero” behavior with a dictionary indexer. The dictionary contains `gold` and `silver`, but no fallback for other tier values. Looking up `standard` throws `KeyNotFoundException`, causing both pricing failures and the HTTP 500 for order `1001`.

The supplied problem-details diagnosis does not match the evidence: `AddProblemDetails` is registered in `Program.cs`, and the existing unknown-order test passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is { } tier ? TierDiscounts.GetValueOrDefault(tier) : 0m;
```

## Why this is the smallest safe fix

It is one find-and-replace in one file. It preserves the intended gold and silver discounts, while restoring the prior zero-discount fallback for every other tier—including a null tier—rather than fixing only `standard`.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All **8 existing tests must pass**: **8 passed, 0 failed**. In particular, the standard-tier pricing tests and `Order_totals_are_available(orderId: 1001)` should pass; the gold and silver tests should continue passing, and unknown orders should continue returning 404.

The pre-fix run showed 3 failures and 5 passes. Verify using the patched repo’s tests, not the deployed localhost API.

## Risks

The patch will not change the deployed 1.4.0 service without a redeploy, which is out of scope. Existing tests do not directly cover arbitrary unlisted tier names, but the proposed fallback restores their previous zero-discount behavior.