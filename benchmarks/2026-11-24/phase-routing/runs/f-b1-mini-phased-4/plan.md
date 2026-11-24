## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The pricing code now does:

```csharp
var discount = TierDiscounts[customer.Tier];
```

But `TierDiscounts` only contains:

```csharp
["gold"] = 0.05m,
["silver"] = 0.02m,
```

So existing `"standard"` customers trigger `KeyNotFoundException`, which surfaces as HTTP 500 on `GET /orders/{id}/total`.

## Change (the exact lines before and after)

**Before** (`src/Orders.Api/Pricing.cs:13-18`)

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
    var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
    var total = subtotal * (1 - discount) * (1 + GstRate);
    return Math.Round(total, 2, MidpointRounding.AwayFromZero);
}
```

## Why this is the smallest safe fix

- It changes only the failing lookup behavior in the one method throwing the exception.
- It preserves the new gold/silver pricing change from 1.4.0.
- It restores the prior behavior for `"standard"` and any other non-discount tier without adding new branching elsewhere.
- It avoids touching controllers, models, or order storage, so the blast radius is minimal.

## Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

It must show:

- All pricing tests pass, including:
  - `Standard_customers_pay_list_price_plus_gst`
  - `Totals_round_to_cents`
  - `Silver_customers_get_two_percent_off`
- The API test passes:
  - `Order_totals_are_available(orderId: 1001)`
- No `KeyNotFoundException` from `Orders.Api.Pricing.Total(...)`

## Risks

- If there are future tiers intended to have discounts but not yet added to the table, they will silently receive 0% off. That matches the old behavior for unknown tiers, so it is low risk for this incident fix.
- If the product wants invalid tier values to fail fast instead of defaulting to list price, that would require a different design change; this fix intentionally does not introduce that behavior.