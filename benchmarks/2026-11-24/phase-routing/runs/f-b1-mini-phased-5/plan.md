## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

```csharp
var discount = TierDiscounts[customer.Tier];
```

This uses a dictionary indexer that throws `KeyNotFoundException` for any tier not present in `TierDiscounts`.

That is the regression introduced by `9bfbf7d` (`Move tier discounts into a lookup table and add silver tier`). The repo still contains a `standard` customer in `src/Orders.Api/OrderStore.cs:8`, and order `1001` belongs to that customer. Because `standard` is not in the dictionary, `GET /orders/1001/total` returns HTTP 500.

## Change (the exact lines before and after)

Change only `src/Orders.Api/Pricing.cs`.

**Before**
```csharp
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
public static decimal Total(Order order, Customer customer)
{
    var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
    var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
    var total = subtotal * (1 - discount) * (1 + GstRate);
    return Math.Round(total, 2, MidpointRounding.AwayFromZero);
}
```

If `GetValueOrDefault` is not available in the target framework, use the equivalent `TryGetValue` fallback:

```csharp
var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

- It changes only the failing code path.
- It preserves the intended behavior for `gold` and `silver`.
- It restores the previous safe behavior for `standard` customers without changing customer data or API behavior elsewhere.
- It avoids introducing a broader refactor when the regression is isolated to dictionary lookup semantics.

## Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

It must show:

- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst` passes
- `Orders.Tests.PricingTests.Totals_round_to_cents` passes
- `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)` passes
- Overall test suite succeeds with no `KeyNotFoundException`

## Risks

- If there are other tiers besides `standard`, `gold`, and `silver`, they will also default to `0m` discount. That matches the prior behavior and is safer than throwing 500s.
- If the intent was to reject unknown tiers explicitly, this fix would preserve availability but not validation. However, the existing tests and old code show that defaulting to no discount is the established behavior.