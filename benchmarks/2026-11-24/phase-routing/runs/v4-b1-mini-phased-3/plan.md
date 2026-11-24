## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The regression is the change from a defaulting conditional to a direct dictionary lookup:

- before: `var discount = customer.Tier == "gold" ? 0.05m : 0m;`
- after: `var discount = TierDiscounts[customer.Tier];`

`TierDiscounts` only contains `gold` and `silver`, but `src/Orders.Api/OrderStore.cs:8` still has a `standard` customer, and order `1001` belongs to that customer. So `Pricing.Total(...)` throws `KeyNotFoundException` for `standard`, which bubbles up as HTTP 500 for `GET /orders/1001/total`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, change only line 16.

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
```

## Why this is the smallest safe fix

- It is a one-line change in one file.
- It restores the last good release’s behavior for every unsupported tier by defaulting to `0m`, which is what 1.3.0 effectively did for all non-gold customers.
- It preserves the intended 1.4.0 change for supported tiers: `gold` still gets `0.05m`, and `silver` still gets `0.02m`.
- It fixes all inputs broken by the regression, not just `standard` and not just order `1001`, because any unknown tier will once again be handled safely instead of throwing.

## Verification (what the existing tests must show after the fix)

`dotnet test` should pass with no failures.

Specifically, the existing tests that currently fail must now pass:

- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
- `Orders.Tests.PricingTests.Totals_round_to_cents`
- `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`

And the remaining existing tests should continue to pass, confirming the silver-tier logic is still intact.

## Risks

- If any future tier should be rejected instead of treated as `0m`, this fix would preserve permissive behavior rather than enforcing validation.
- The change assumes the intended behavior for unknown tiers is “no discount,” which matches the pre-1.4.0 release and the failing tests, but it does not add explicit validation or logging.
- If `TierDiscounts.GetValueOrDefault` is unavailable in the target framework or code style, the implementer will need the equivalent safe lookup pattern; the behavior should remain the same.