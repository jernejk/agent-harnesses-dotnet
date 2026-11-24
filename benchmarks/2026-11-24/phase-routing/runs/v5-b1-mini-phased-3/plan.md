The tests and code match the incident report exactly: the dictionary index on `customer.Tier` is crashing for `standard`. I can now give the smallest safe one-line fix plan and stop there, since no further checking is needed.
## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The regression changed pricing from a safe fallback to an unsafe dictionary index:

- before: non-gold tiers effectively got `0m` discount
- after: `TierDiscounts[customer.Tier]`

That throws `KeyNotFoundException` for existing tiers not in the table, especially `standard`, which is why `GET /orders/1001/total` now returns HTTP 500.

## Change (the exact lines before and after)

Replace this line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with this line:

```csharp
var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

- It changes only one line in one file.
- It restores the last good behavior for every unknown/non-discounted tier by giving them `0m` discount, which matches 1.3.0.
- It keeps the intended 1.4.0 behavior for `gold` and `silver` customers because those tiers still resolve from the lookup table.
- It avoids introducing new branching logic or data shape changes, so it is the minimum change that fixes all inputs affected by the regression.

## Verification (what the existing tests must show after the fix)

After the fix, `dotnet test` should show:

- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst` passes
- `Orders.Tests.PricingTests.Totals_round_to_cents` passes
- `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)` passes
- overall test run is green

## Risks

- Any future tier added to the data but not to `TierDiscounts` will again receive `0m` discount, which matches prior behavior but may hide missing configuration.
- If 1.4.0 intended every tier to be explicitly discounted, this fix preserves compatibility over strict enforcement.