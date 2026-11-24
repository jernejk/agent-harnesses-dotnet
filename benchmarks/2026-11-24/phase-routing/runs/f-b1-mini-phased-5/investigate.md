Symptom
- `GET /orders/{id}/total` returns HTTP 500 in 1.4.0.
- Local reproduction: `/orders/1001/total -> 500`.
- Tests fail in `PricingTests` and `OrdersApiTests`.

Reproduction
- `http_get /orders/1001/total` returned `500`.
- `dotnet test` failed with:
  - `System.Collections.Generic.KeyNotFoundException: The given key 'standard' was not present in the dictionary.`
  - Failure originates in `Orders.Api.Pricing.Total` at `src/Orders.Api/Pricing.cs:16`.

Change that caused it (commit and subject)
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- `var discount = TierDiscounts[customer.Tier];`
- The new lookup table only contains `"gold"` and `"silver"`, but existing customers include `"standard"` in `src/Orders.Api/OrderStore.cs:8`.
- When an order belongs to a standard customer (e.g. order 1001), the dictionary indexer throws `KeyNotFoundException`, which bubbles up as HTTP 500.
- In v1.3.0, standard customers were handled by the previous logic (`customer.Tier == "gold" ? 0.05m : 0m`), so they did not crash.

Evidence
- `dotnet test` shows:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst` failing with `KeyNotFoundException`
  - `Orders.Tests.PricingTests.Totals_round_to_cents` failing with the same exception
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)` returning `InternalServerError`
- `git log v1.3.0..HEAD` shows the relevant release-range commits:
  - `f1812c8 Release 1.4.0`
  - `ab38e53 Return problem details for unknown orders`
  - `9bfbf7d Move tier discounts into a lookup table and add silver tier`
- `git show v1.3.0:src/Orders.Api/Pricing.cs` confirms the old code had safe default behavior for non-gold tiers.
- `git show v1.3.0:src/Orders.Api/OrderStore.cs` shows standard customers existed before the regression.

Confidence
- High. The failure is directly reproduced, the stack trace names the exact line, and the change set clearly introduced an unsupported `"standard"` tier lookup.