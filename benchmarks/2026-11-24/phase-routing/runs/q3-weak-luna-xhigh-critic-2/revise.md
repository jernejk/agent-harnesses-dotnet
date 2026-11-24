## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–11,16`: `Pricing.Total` looks up the customer’s discount with `TierDiscounts[customer.Tier]`, but the dictionary includes `"gold"` and `"silver"` and omits `"standard"`.

`src/Orders.Api/OrderStore.cs:7–9,14–16` shows the service’s seeded customers and orders. Order `1001` belongs to the `"standard"` customer, so the lookup throws `KeyNotFoundException`. The same lookup explains the failures in the standard-tier pricing tests.

Commit `9bfbf7d` introduced this regression: it replaced the previous “gold gets a discount; otherwise zero” logic with the dictionary lookup, without adding the standard tier. The Problem Details change is not the cause: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`, and the failing request reaches `Pricing.Total` for an existing order.

The reviewer is right to ask which tiers are reachable. The current store supplies exactly three—gold, standard, and silver. Existing pricing tests assert the expected results for all three, and the API test covers the corresponding three seeded order IDs. Unknown or null tiers have no defined pricing policy and are not supplied by the current store.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier with no discount.

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

No additional test cases are needed for the currently reachable tiers: `PricingTests.cs:12–22` already checks standard, gold, and silver pricing, and `OrdersApiTests.cs:9–17` exercises the total endpoint for all three seeded orders. Keep those tests unchanged.

Do not add a fallback for unknown or null tiers without an explicit pricing policy. They are not reachable through the current seeded endpoint data, and silently treating an unrecognized tier as standard could misprice future data.

## Why this is the smallest safe fix

The added entry restores the prior zero-discount behavior for standard customers while retaining the existing gold and silver rates. It changes neither the calculation nor endpoint response handling, and it covers every tier currently supplied by `OrderStore`.

## Verification (what to run and what it must show)

From the repository root, run:

```text
dotnet test
```

The current baseline is **3 failed and 5 passed**. The failing cases are `Standard_customers_pay_list_price_plus_gst`, `Totals_round_to_cents`, and `Order_totals_are_available(orderId: 1001)`; the observed exception is `KeyNotFoundException` for `"standard"` at `Pricing.cs:16`.

After the change, the test run must show **all 8 tests passing**, including the existing standard, gold, and silver pricing tests and the API total cases for orders 1001–1003.

Do not verify against localhost: it is still running the deployed 1.4.0 build, and redeployment is out of scope.## Risks

The direct dictionary lookup will still throw for an unlisted tier; a null tier will also fail. Neither is supplied by the current store. If tiers later come from another data source, define validation and pricing behavior for unknown or null values and test that contract rather than silently assigning a discount.