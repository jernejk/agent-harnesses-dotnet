## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–16`: commit `9bfbf7d` replaced the zero-discount fallback with a dictionary lookup but omitted the `standard` tier. Order 1001 belongs to a standard customer, so the lookup throws `KeyNotFoundException` and the total endpoint returns 500.

The reported problem-details cause is not supported: `AddProblemDetails()` is registered at `Program.cs:5`, and the unknown-order 404 test passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:7–11`:

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

It adds the missing zero-discount policy without changing gold or silver pricing or the unknown-order response. Existing tests already cover standard pricing and the affected endpoint.

## Verification (what to run and what it must show)

After applying the change, run `dotnet test`. All **8 tests must pass**, including standard pricing and rounding, gold and silver pricing, HTTP 200 for order 1001, and the unknown-order 404.

The current unmodified repo produces 3 failures and 5 passes. Do not use the live API to verify the patch: it remains on the deployed build until redeployment.

## Risks

Unrecognized tiers will still throw; unlike 1.3.0, they do not default to zero discount. This plan deliberately avoids silently pricing unknown tiers. The API test checks status rather than the returned total amount, although the pricing tests check calculated amounts.