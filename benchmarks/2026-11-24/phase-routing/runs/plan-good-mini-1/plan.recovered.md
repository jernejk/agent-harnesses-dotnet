Here is the smallest safe fix plan for the engineer.

## Root cause (file:line)
- `src/Orders.Api/Pricing.cs:16`
- The code now does `TierDiscounts[customer.Tier]`.
- `TierDiscounts` only contains `gold` and `silver` at lines 7–11, but the system still has `standard` customers, including order `1001` in `src/Orders.Api/OrderStore.cs:8,14`.
- Because `standard` is missing from the dictionary, pricing throws `KeyNotFoundException`, which surfaces as HTTP 500 on `GET /orders/{id}/total`.

## Change (the exact lines before and after)
Change only `src/Orders.Api/Pricing.cs`.

### Before
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

### After
```csharp
static readonly Dictionary<string, decimal> TierDiscounts = new()
{
    ["standard"] = 0.00m,
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

## Why this is the smallest safe fix
- It preserves the current lookup-table design introduced by the incident-causing commit.
- It restores the previously working `standard` behavior without changing pricing formulas, rounding, routing, or API error handling.
- It is a one-line functional data fix: add the missing `standard` entry with the correct zero discount.
- It directly resolves the crashing path for order `1001` and the failing pricing tests.

## Verification (what to run and what it must show)
Run:

1. `dotnet test`
   - Must pass all tests.
   - In particular, the two pricing tests must no longer throw `KeyNotFoundException`.
   - The API test for order `1001` must return success instead of `InternalServerError`.

2. If there is a targeted API test command in the repo, run it as well; otherwise the full test suite is enough.
   - Must show `GET /orders/1001/total` returns the expected total for a standard-tier customer.

3. Optional smoke check if available in the test harness:
   - `GET /health`
   - Must still return `200` and report version `1.4.0`.

## Risks
- Very low risk: adding `standard => 0.00m` only restores an existing business rule.
- If there are other customer tiers in production data not covered by the table, they would still fail; this fix only addresses the known regression.
- If future pricing tiers are added, tests should be updated to ensure every supported tier is explicitly represented in the discount table.