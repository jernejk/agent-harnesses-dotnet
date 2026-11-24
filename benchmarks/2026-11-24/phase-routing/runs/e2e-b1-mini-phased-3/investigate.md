I’ve confirmed the faulting line and the matching release change that introduced it.
Symptom
- GET `/orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.
- Local reproduction: `GET /orders/1001/total -> 500`.
- Test failure shows `KeyNotFoundException` for customer tier `standard`.

Reproduction
- `dotnet test` fails in:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `Orders.Tests.PricingTests.Totals_round_to_cents`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available`
- Runtime/API repro:
  - `http_get /orders/1001/total` returned `500`.

Change that caused it (commit and subject)
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`
- This is the regression window between `v1.3.0` and `HEAD`; the 1.4.0 release includes that commit.

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- `var discount = TierDiscounts[customer.Tier];`
- The lookup table only contains `gold` and `silver`, but the order store still has a `standard` customer (`src/Orders.Api/OrderStore.cs:8`), so pricing throws `KeyNotFoundException` when calculating totals for standard customers.

Evidence
- `dotnet test` stack trace:
  - `System.Collections.Generic.KeyNotFoundException : The given key 'standard' was not present in the dictionary.`
  - at `Orders.Api.Pricing.Total(...) in .../Pricing.cs:line 16`
- `git show`/`blame` show the discount dictionary was introduced in commit `9bfbf7d`.
- `OrderStore.cs` still defines customer `2` as tier `standard`.
- API call to `/orders/1001/total` returns 500 because order `1001` belongs to customer `2`.

Confidence
- High. The failing tests, API reproduction, and source inspection all point to the same missing `standard` entry in the pricing lookup.