Here is the implementation plan based on the code and test evidence.

## Root cause (file:line)
`src/Orders.Api/Pricing.cs:16`

`var discount = TierDiscounts[customer.Tier];`

The 1.4.0 change replaced the prior conditional logic with a dictionary lookup, but the lookup table only contains `gold` and `silver`. The codebase still has `standard` customers, including order `1001` via `src/Orders.Api/OrderStore.cs:8`, so `Pricing.Total` throws `KeyNotFoundException` for that tier and the API returns HTTP 500.

## Change (the exact lines before and after)
Change `src/Orders.Api/Pricing.cs` only.

**Before**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };

    public static decimal Total(Order order, Customer customer)
    {
        var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = TierDiscounts[customer.Tier];
        var total = subtotal * (1 - discount) * (1 + GstRate);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
```

**After**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };

    public static decimal Total(Order order, Customer customer)
    {
        var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = TierDiscounts[customer.Tier];
        var total = subtotal * (1 - discount) * (1 + GstRate);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
```

## Why this is the smallest safe fix
- It preserves the 1.4.0 lookup-table refactor.
- It restores behavior for the existing `standard` tier without changing any API shape, routing, or data model.
- It is a one-line data correction in the new lookup table, which is the narrowest change that addresses the crash.
- The tests and runtime failure both point to the same missing key, so fixing the table entry directly resolves the incident with minimal risk.

## Verification (what to run and what it must show)
Run:

```bash
dotnet test
```

It must show:
- all tests pass, especially:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `Orders.Tests.PricingTests.Totals_round_to_cents`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`
- no `KeyNotFoundException` for tier `standard`
- HTTP 500 is no longer returned for `GET /orders/1001/total`

## Risks
- If `standard` is not the only legacy tier missing from the lookup, this fix will only address the currently failing path.
- Using a dictionary lookup still leaves the code vulnerable to future unknown tiers unless new tiers are added consistently.
- This keeps the current behavior of explicit tier mapping, which is correct for the incident but does not add defensive fallback handling.