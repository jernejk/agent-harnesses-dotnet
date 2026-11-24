## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts[customer.Tier]`, but the dictionary at lines 7–11 has entries only for `"gold"` and `"silver"`. Order 1001 belongs to a `"standard"` customer, so the lookup throws `KeyNotFoundException` and the endpoint returns HTTP 500.

Commit `9bfbf7d` introduced this regression by replacing the previous zero-discount fallback for non-gold customers with the dictionary lookup, without adding a `"standard"` entry. The reported `Program.cs:30` cause is unsupported: `AddProblemDetails()` is registered at line 5, and the failing order takes the successful-order path, not the not-found helper.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add a zero-discount entry to the table.

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

It restores the prior behavior for standard customers while preserving the existing gold and silver discounts. The existing unit tests cover standard pricing, and the API test for order 1001 covers the affected endpoint; no change to the not-found response path is needed.

## Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

It must show all 8 tests passing, including `Standard_customers_pay_list_price_plus_gst`, `Totals_round_to_cents`, and `Order_totals_are_available(orderId: 1001)`. The unknown-order 404 test should continue to pass. The current run reproduces the incident: 3 failed, 5 passed, with the standard-tier lookup failures showing `KeyNotFoundException`.

## Risks

The dictionary will still throw for an unrecognized customer tier. The proposed change covers the tiers and behavior present in the repository; defining behavior for arbitrary unknown tiers is outside this minimal fix. The deployed 1.4.0 service will remain unaffected by a repository patch until a redeploy; verification should use `dotnet test`, not the live API.