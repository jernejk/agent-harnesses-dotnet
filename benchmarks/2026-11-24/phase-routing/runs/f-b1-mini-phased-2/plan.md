I’ve confirmed the root cause and the exact lines. The smallest safe fix is to restore the old behavior for unknown tiers while keeping the new lookup for known tiers. I’m going to write the implementation plan in the required format now; no code changes will be made.
Root cause (file:line)
- `src/Orders.Api/Pricing.cs:16`
- The pricing logic now does `TierDiscounts[customer.Tier]`, which throws `KeyNotFoundException` for tiers not present in the lookup.
- This is confirmed by the current code: `TierDiscounts` only contains `"gold"` and `"silver"` at lines 7–10, while `src/Orders.Api/OrderStore.cs:8` still has a `"standard"` customer used by order `1001` at line 14.
- The tests also prove the contract: `tests/Orders.Tests/PricingTests.cs:13-15` expects standard customers to pay list price plus GST, not to fail.

Change (the exact lines before and after)
- File: `src/Orders.Api/Pricing.cs`

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

Why this is the smallest safe fix
- It changes only one line in one file.
- It preserves the new discount table for supported tiers.
- It restores the previous safe fallback behavior for any unknown or legacy tier, which matches the existing tests and the historical behavior before commit `9bfbf7d8...`.
- It avoids broader changes to order data, customer data, or endpoint handling.

Verification (what to run and what it must show)
- Run: `dotnet test`
- Must show:
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available` passes, meaning `GET /orders/{id}/total` no longer returns 500 for the standard customer in order `1001`.
  - `Orders.Tests.PricingTests.*` all pass, including the standard-customer case.
  - No `KeyNotFoundException` from `Orders.Api.Pricing.Total`.

Risks
- Any tier that was intentionally meant to be unsupported will now silently receive 0% discount instead of failing. This matches prior behavior and the current test contract, so it is low risk.
- If future requirements need explicit rejection of unknown tiers, that should be a separate change with API-level behavior defined first.