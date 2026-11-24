## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` by the customer tier, but the table at lines 7–11 has no `"standard"` entry. Order `1001` belongs to a standard-tier customer, so the lookup throws `KeyNotFoundException` and the totals endpoint returns HTTP 500.

The supplied findings point to the wrong cause. Commit `ab38e53` added `AddProblemDetails()` in `Program.cs` and uses the `NotFound` helper only for unknown orders; it does not affect the successful path for order `1001`. The missing tier entry was introduced in `9bfbf7d`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry to the existing dictionary.

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

It adds the missing mapping for a valid, seeded customer tier and preserves the expected zero-discount behavior for standard customers. It avoids changing the endpoint or problem-details handling, neither of which is responsible for this failure.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

I ran it against the unmodified repo: it reproduced **3 failed, 5 passed**. The failures include the standard-tier pricing tests and `Order_totals_are_available(orderId: 1001)`; the API failure’s stack trace points to `Pricing.cs:16`.

After the change, the test run should show **8 passed, 0 failed**, including the 1001 total test and the unknown-order 404 test. Do not use the localhost API for verification: it remains the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

Unrecognized tier values will still throw on dictionary lookup. This change intentionally covers the known `"standard"` tier only; handling arbitrary unknown tiers would be a broader behavior change. No files were modified.