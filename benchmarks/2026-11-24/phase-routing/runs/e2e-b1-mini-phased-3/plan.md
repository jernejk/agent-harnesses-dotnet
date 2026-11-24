Done — the plan is complete and the todo item has been marked finished.

## Root cause (file:line)
- `src/Orders.Api/Pricing.cs:16`
- The code does `var discount = TierDiscounts[customer.Tier];`
- `TierDiscounts` no longer includes `standard`, but `src/Orders.Api/OrderStore.cs:8` still has a customer with tier `"standard"`.
- So `GET /orders/{id}/total` throws `KeyNotFoundException` for existing orders that belong to that customer.

## Change (the exact lines before and after)
In `src/Orders.Api/Pricing.cs`, update the discount lookup table to include the existing `standard` tier.

**Before**
```csharp
private static readonly IReadOnlyDictionary<string, decimal> TierDiscounts =
    new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["gold"] = 0.10m,
        ["silver"] = 0.05m,
    };
```

**After**
```csharp
private static readonly IReadOnlyDictionary<string, decimal> TierDiscounts =
    new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["standard"] = 0.00m,
        ["gold"] = 0.10m,
        ["silver"] = 0.05m,
    };
```

## Why this is the smallest safe fix
- It preserves the current pricing behavior for `gold` and `silver`.
- It restores support for the existing data model without changing stored orders or customer tiers.
- It avoids broader behavior changes like switching to a fallback lookup or modifying exception handling.
- It directly fixes the observed `KeyNotFoundException` with the minimum code change.

## Verification (what to run and what it must show)
Run:
- `dotnet test`

It must show:
- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst` passes
- `Orders.Tests.PricingTests.Totals_round_to_cents` passes
- `Orders.Tests.OrdersApiTests.Order_totals_are_available` passes
- No `KeyNotFoundException` for tier `standard`

## Risks
- If `standard` was intentionally removed as part of a pricing policy change, re-adding it as `0.00m` would restore prior behavior rather than enforcing the new policy.
- If other tiers exist in production data but are not represented here, they could cause the same failure later; this fix addresses only the confirmed missing tier.