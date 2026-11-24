### Symptom
- API `/orders/{id}/total` returns `500 InternalServerError` for valid orders.
- Unit tests fail in `Orders.Tests.PricingTests` with `KeyNotFoundException` for customer tier `standard`.

### Reproduction
- Ran `dotnet test`
- Failing tests:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `Orders.Tests.PricingTests.Totals_round_to_cents`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`
- Runtime error:
  - `System.Collections.Generic.KeyNotFoundException : The given key 'standard' was not present in the dictionary.`
- API check:
  - `/health` is OK and reports version `1.4.0`

### Change that caused it (commit and subject)
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

### Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- The code changed from a safe conditional default:
  - `customer.Tier == "gold" ? 0.05m : 0m`
- To an unsafe dictionary lookup:
  - `var discount = TierDiscounts[customer.Tier];`
- The lookup table only contains `gold` and `silver`, but existing customers/orders still use `standard`, so pricing throws `KeyNotFoundException` instead of treating unknown tiers as zero-discount.

### Evidence
- `git log v1.3.0..HEAD` shows the introducing commit `9bfbf7d`
- `git show 9bfbf7d -- src/Orders.Api/Pricing.cs` shows the new dictionary lookup and missing `standard`
- `git show 9bfbf7d -- src/Orders.Api/OrderStore.cs` shows customer tiers still include `standard`
- `git grep -n "standard" -- src tests` confirms tests and seeded data still depend on that tier
- `dotnet test` reproduces the exact exception at `Pricing.cs:16`

### Confidence
- High. The failure is directly reproduced, the regression commit is identified, and the code path causing the exception is explicit.