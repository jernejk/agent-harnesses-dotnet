I have enough to draft the minimal fix plan. The evidence supports the finding exactly: `Pricing.Total` throws for `"standard"` because the dictionary removed that key, while `OrderStore` and tests still rely on standard pricing. I’ll write the engineer-facing plan now, with no code changes applied.
Planned fix for the engineer:

## Root cause (file:line)
`src/Orders.Api/Pricing.cs:16`

Current code:
```csharp
var discount = TierDiscounts[customer.Tier];
```

Why this is the cause:
- `TierDiscounts` only contains `"gold"` and `"silver"` at lines 7–11.
- `src/Orders.Api/OrderStore.cs:8` still has a `"standard"` customer.
- `tests/Orders.Tests/PricingTests.cs:13-26` still expects standard customers to price successfully.
- So any order for a standard-tier customer hits a `KeyNotFoundException` and surfaces as HTTP 500.

## Change (the exact lines before and after)
Change only `src/Orders.Api/Pricing.cs`.

Before:
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

After:
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };

    public static decimal Total(Order order, Customer customer)
    {
        var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
        var total = subtotal * (1 - discount) * (1 + GstRate);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
```

## Why this is the smallest safe fix
- It restores the previous behavior for `"standard"` customers without changing any customer data or tier definitions.
- It keeps the new lookup-table design for supported tiers (`gold`, `silver`).
- It changes only one line in one file, so the risk of side effects is minimal.
- It matches the existing test intent: standard customers should pay list price plus GST.

## Verification (what to run and what it must show)
Run:
```bash
dotnet test
```

It must show:
- `tests/Orders.Tests/PricingTests.cs` passes, especially the standard-customer cases.
- `tests/Orders.Tests/OrdersApiTests.cs` passes, including `GET /orders/1001/total` returning `200`.
- No new failures are introduced.

## Risks
- Any future unrecognized tier will also fall back to `0m`, which may hide data issues if a typo or new tier is introduced without a corresponding discount.
- If the intended business rule is to reject unknown tiers instead of defaulting to standard pricing, this fix would preserve compatibility but not enforce that stricter rule.