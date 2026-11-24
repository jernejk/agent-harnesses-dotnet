The test failures confirm the regression is exactly the unsafe indexer: both unit tests and the API test fail on the `standard` tier customer in order `1001`. The smallest fix is therefore to keep the new dictionary but use a safe fallback for tiers not present, which restores the prior default-to-zero behavior for `standard` and any future unknown tier. I’ll now draft the plan with the exact before/after line and the verification that the existing tests should pass once that one-line change is made.
## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The regression is the direct dictionary indexer lookup:

```csharp
var discount = TierDiscounts[customer.Tier];
```

This throws `KeyNotFoundException` for any customer tier not present in the new lookup table. The repo still has an order for customer tier `standard` in `src/Orders.Api/OrderStore.cs`, so `/orders/1001/total` crashes on the current 1.4.0 code path.

This matches the report and the test failures:
- `PricingTests.Standard_customers_pay_list_price_plus_gst`
- `PricingTests.Totals_round_to_cents`
- `OrdersApiTests.Order_totals_are_available(orderId: 1001)`

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, change only line 16.

**Before:**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After:**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

- It keeps the 1.4.0 intended change: the tier discount table still exists, including the new `silver` tier.
- It restores the pre-1.4.0 behavior for every input that regressed: unknown tiers now get a default discount of `0m` instead of crashing.
- It is a one-line, one-file change, which matches the implementation constraint.
- It does not alter order lookup, routing, or response formatting; it only prevents the pricing calculation from throwing.

## Verification (what the existing tests must show after the fix)

After the change, `dotnet test` should pass all existing tests, specifically:
- `PricingTests.Standard_customers_pay_list_price_plus_gst`
- `PricingTests.Totals_round_to_cents`
- `OrdersApiTests.Order_totals_are_available(orderId: 1001)`

The important signal is that the tests no longer fail with `KeyNotFoundException`, and the API test returns `200 OK` for the order total endpoint instead of `500 InternalServerError`.

## Risks

- `GetValueOrDefault` defaults unknown tiers to `0m`. That matches the old behavior, but if future business rules want unknown tiers rejected instead of priced at list rate, this would not enforce that.
- If there are hidden tests expecting an exception for invalid tiers, this change would intentionally keep the previous tolerant behavior rather than surfacing an error.
- No other code paths are changed, so the risk of unintended side effects is low.