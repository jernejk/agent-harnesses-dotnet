## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`: `Pricing.Total` indexes `TierDiscounts` using the customer’s tier. The dictionary at lines 7–11 has entries for `"gold"` and `"silver"`, but not `"standard"`.

Order 1001 belongs to a `"standard"` customer (`src/Orders.Api/OrderStore.cs:8,14`), so its total calculation throws `KeyNotFoundException` and the API returns 500. The test run reproduced this along with two failing standard-tier pricing tests.

The investigation finding about Problem Details is not supported: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`, and the `NotFound` helper is used only when an order is absent. The regression is the tier lookup introduced in commit `9bfbf7d`, not the not-found response change in `ab38e53`.

## Change (the exact lines before and after)

Add an explicit zero-discount entry for the supported standard tier in `src/Orders.Api/Pricing.cs`.

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
        ["standard"] = 0m,
        ["silver"] = 0.02m,
    };
```

## Why this is the smallest safe fix

It adds one mapping for a tier already used by the service, restoring standard customers’ zero-discount behavior while leaving gold and silver pricing and endpoint response handling unchanged. Existing tests already cover standard pricing and the order-total endpoint for order 1001.

## Verification (what to run and what it must show)

From the repository root, run:

```text
dotnet test
```

It must show all 8 tests passing, including `Standard_customers_pay_list_price_plus_gst`, `Totals_round_to_cents`, and `Order_totals_are_available(orderId: 1001)`. The current baseline is 3 failed and 5 passed; the failures are those three standard-tier cases.

Do not use the localhost API as verification: it is still the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

The direct dictionary lookup will still throw for any tier not explicitly listed. The current store uses only gold, standard, and silver; if other tier values are valid, their intended discount should be defined separately.