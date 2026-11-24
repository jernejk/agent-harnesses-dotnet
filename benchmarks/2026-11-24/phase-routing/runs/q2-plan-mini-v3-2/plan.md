## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

```csharp
var discount = TierDiscounts[customer.Tier];
```

This direct dictionary index throws `KeyNotFoundException` for `standard` customers because the table at lines 7–10 only contains `gold` and `silver`.

Supporting evidence:
- `src/Orders.Api/OrderStore.cs:8,14` shows order `1001` belongs to customer `2`, whose tier is `standard`.
- `dotnet test` reproduces the bug:
  - `PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `PricingTests.Totals_round_to_cents`
  - `OrdersApiTests.Order_totals_are_available(orderId: 1001)`

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, change line 16 from:

```csharp
var discount = TierDiscounts[customer.Tier];
```

to:

```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
```

No other code changes are needed.

## Why this is the smallest safe fix

- It restores the pre-1.4.0 behavior implied by the incident notes: tiers without an explicit discount should get `0m`.
- It fixes all failing paths at once:
  - standard pricing tests
  - rounding test
  - `/orders/1001/total` API failure
- It is narrowly scoped to one line and does not change the pricing model for `gold` or `silver`.
- It avoids introducing new branching or altering the discount table structure.

## Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

It must show:
- all tests pass
- specifically, the three previously failing tests now pass
- no `KeyNotFoundException` for `'standard'`
- the API test for order `1001` returns `OK` instead of `InternalServerError`

## Risks

- Any future tier not present in `TierDiscounts` will now default to `0m` rather than failing fast. That matches the prior behavior, but it also means missing-tier additions could go unnoticed unless covered by tests.
- If the product intent was to require explicit discounts for all tiers, this fix would mask that requirement. The current evidence, however, strongly supports the fallback behavior as the intended one.