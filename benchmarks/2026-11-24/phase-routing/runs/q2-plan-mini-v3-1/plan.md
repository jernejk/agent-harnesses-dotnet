Root cause (file:line)

- `src/Orders.Api/Pricing.cs:16`
- `Pricing.Total` uses `TierDiscounts[customer.Tier]`, but `TierDiscounts` only contains `gold` and `silver`.
- Order `1001` is for customer `2`, whose tier is `standard` in `src/Orders.Api/OrderStore.cs:8,14`.
- Because `standard` is not in the dictionary, the lookup throws `KeyNotFoundException`, which bubbles up as HTTP 500.

Change (the exact lines before and after)

Current lines:
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

Replace with:
```csharp
static readonly Dictionary<string, decimal> TierDiscounts = new()
{
    ["gold"] = 0.05m,
    ["silver"] = 0.02m,
};

public static decimal Total(Order order, Customer customer)
{
    var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
    var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
    var total = subtotal * (1 - discount) * (1 + GstRate);
    return Math.Round(total, 2, MidpointRounding.AwayFromZero);
}
```

Why this is the smallest safe fix

- It restores the pre-1.4.0 behavior: known discounted tiers get their discount, and all other tiers get zero discount.
- It changes only one line in one file.
- It does not alter the discount table, order data, API shape, or rounding logic.
- It matches the existing test intent: `standard` customers pay list price plus GST, not a special discount.

Verification (what to run and what it must show)

- Run: `dotnet test`
- Must show:
  - all tests pass
  - no `KeyNotFoundException` for tier `standard`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)` returns `OK`
- Expected result: `8 passed, 0 failed`

Risks

- Very low risk: `GetValueOrDefault` returns `0m` for unknown tiers, which is the same effective behavior the service had before the regression.
- If there are future tiers that should be discounted, they still need explicit entries in `TierDiscounts`.
- This fix assumes the intended default for non-listed tiers is zero discount, which is supported by the tests and the prior behavior.