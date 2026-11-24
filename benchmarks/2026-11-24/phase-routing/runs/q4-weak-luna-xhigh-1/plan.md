## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with the customer’s tier. The 1.4.0 dictionary contains `gold` and `silver`, but not `standard`, so pricing order `1001` throws `KeyNotFoundException` and the endpoint returns HTTP 500.

The reported cause is not supported: `Program.cs` registers `AddProblemDetails()` on line 5, and its `NotFound` helper is used for unknown orders—not the successful path for order `1001`. The test failure stack trace confirms the exception originates in `Pricing.Total`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this single line:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is a one-line, one-file replacement. It preserves the intended 1.4.0 discounts for `gold` and `silver`, while restoring the previous zero-discount fallback for `standard` and any other tier missing from the lookup. The null check also preserves the previous behavior for a null tier rather than allowing a dictionary lookup to throw.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. It should report **8 passed, 0 failed**, including the standard-tier pricing tests, the silver-tier pricing test, and `Order_totals_are_available` for orders 1001, 1002, and 1003. The unknown-order test should continue to return 404.

The current run has 3 failures and 5 passes; I did not apply the fix or run tests against a modified version.

## Risks

The existing tests do not directly cover arbitrary unmapped tiers or a null tier, although the proposed fallback handles both. The repository change will not affect the already-deployed 1.4.0 service without a redeploy; verification here is with `dotnet test`, not the live API.