## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The incident report is correct. `Pricing.Total(...)` now does:

```csharp
var discount = TierDiscounts[customer.Tier];
```

but `TierDiscounts` only contains:

- `gold`
- `silver`

Existing customers in `src/Orders.Api/OrderStore.cs` still include `standard`, and the tests explicitly expect `standard` to price successfully. That makes the dictionary lookup throw `KeyNotFoundException`, which surfaces as HTTP 500 on `GET /orders/{id}/total`.

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

## Why this is the smallest safe fix

- It changes only one line in one file.
- It preserves the new silver-tier behavior.
- It restores the pre-1.4.0 behavior for unknown or legacy tiers by defaulting to `0m` discount.
- It avoids changing customer data, tests, or API behavior beyond fixing the crash.
- It is consistent with the previous implementation, which treated non-gold tiers as no-discount.

## Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

It must show:

- `Orders.Tests.OrdersApiTests.Order_totals_are_available` passes
- `PricingTests` passes, including:
  - `standard` tier returns no discount
  - `gold` tier returns 5% off
  - `silver` tier returns 2% off
- No `KeyNotFoundException` from `Pricing.Total(...)`

## Risks

- If future tiers are added without explicit discounts, they will still default to `0m`, which is probably desirable but should be a conscious product choice.
- This fix does not add an explicit test for unknown tiers; existing `standard` coverage is enough to protect this regression, but a separate test could be added later if desired.