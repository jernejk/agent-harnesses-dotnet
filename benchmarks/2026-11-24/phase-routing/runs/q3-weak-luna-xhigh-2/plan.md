## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer’s tier, but the lookup table at lines 7–11 has no `"standard"` entry. Order `1001` belongs to a standard-tier customer, so the lookup throws `KeyNotFoundException`; the endpoint consequently returns HTTP 500.

This omission was introduced by `9bfbf7d` when tier discounts moved into a lookup table. The proposed problem-details cause is not supported: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`, and the test for an unknown order passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add a zero-discount entry to the existing table.

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

It restores the existing zero-discount behavior for standard customers without changing endpoint handling, pricing formulas, or the intended gold and silver discounts. The repo already has tests covering standard-tier pricing and a standard-tier order.

## Verification (what to run and what it must show)

Run `dotnet test`. It must finish with **8 passed, 0 failed**. In particular, the standard-pricing tests and `Order_totals_are_available(orderId: 1001)` must pass; the unknown-order 404 test must remain passing.

Do not use the live API for verification: the deployed 1.4.0 build will not include this repo patch without a redeploy.

## Risks

Unknown or newly introduced tier names will still throw at the dictionary lookup. This fix intentionally handles the known `"standard"` tier only; broader behavior for unsupported tiers is outside this incident’s scope. No files were changed.