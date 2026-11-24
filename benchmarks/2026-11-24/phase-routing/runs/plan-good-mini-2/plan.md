I have enough to write the fix plan. I’m going to keep it minimal: preserve the new lookup table and restore the old default behavior only for missing tiers, since that matches the pre-regression semantics and directly fixes standard customers without affecting gold/silver.
I verified the regression in `src/Orders.Api/Pricing.cs` and confirmed it matches the incident symptoms.

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

Current code:
```csharp
var discount = TierDiscounts[customer.Tier];
```

Why it breaks:
- `TierDiscounts` only contains `"gold"` and `"silver"` at lines 7–11.
- Standard-tier customers exist in the store (`src/Orders.Api/OrderStore.cs:8`), including order `1001` (`src/Orders.Api/OrderStore.cs:14`).
- Looking up `"standard"` with the indexer throws `KeyNotFoundException`, causing the HTTP 500 and the failing pricing tests.

## Change (the exact lines before and after)

### Before
```csharp
    public static decimal Total(Order order, Customer customer)
    {
        var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = TierDiscounts[customer.Tier];
        var total = subtotal * (1 - discount) * (1 + GstRate);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
```

### After
```csharp
    public static decimal Total(Order order, Customer customer)
    {
        var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
        var total = subtotal * (1 - discount) * (1 + GstRate);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
```

If the codebase target/runtime does not support `GetValueOrDefault` on this dictionary type, use the equivalent explicit fallback:
```csharp
        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
            ? tierDiscount
            : 0m;
```

## Why this is the smallest safe fix

- It changes only one line in one file.
- It restores the pre-1.4.0 behavior: unknown tiers receive `0m` discount.
- It fixes all currently failing cases:
  - standard-tier pricing tests
  - order-total API for order `1001`
- It preserves the new tier table for `gold` and `silver` without changing the lookup data or broader pricing logic.

## Verification (what to run and what it must show)

Run:
```bash
dotnet test
```

Must show:
- 0 failed tests
- the standard-tier pricing test passes
- the order-total API test for order `1001` returns success / expected total instead of HTTP 500

If there is a targeted API test command in the repo, also verify the endpoint manually or through that test:
- `GET /orders/1001/total` should return HTTP 200 with the expected total
- `GET /health` should remain HTTP 200

## Risks

- This preserves the old “unknown tier = no discount” behavior. If a future tier should receive a discount, it must be added to the table explicitly.
- If there are hidden tests expecting a failure for unknown tiers, this change would alter that behavior; however, the incident evidence and prior version behavior both indicate fallback-to-zero is intended.
- If the runtime/API target doesn’t support `GetValueOrDefault`, the explicit `TryGetValue` form should be used instead.