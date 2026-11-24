Symptom
- `GET /orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.
- Local repro: `/orders/1001/total` returns `500`.
- Tests also fail with `KeyNotFoundException: The given key 'standard' was not present in the dictionary.`

Reproduction
- `http_get /orders/1001/total` → 500
- `dotnet test` → 3 failures
- The failing tests point to `Orders.Api.Pricing.Total(...)` throwing on `customer.Tier == "standard"`.

Change that caused it (commit and subject)
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`
- This is the release change in `v1.4.0` that introduced the regression.

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- The code now does `TierDiscounts[customer.Tier]`, but `TierDiscounts` only contains `"gold"` and `"silver"`.
- Existing `"standard"` customers are not in the dictionary, so pricing throws `KeyNotFoundException`, which surfaces as HTTP 500 on `/orders/{id}/total`.

Evidence
- `dotnet test` failure:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `Orders.Tests.PricingTests.Totals_round_to_cents`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`
- Stack trace shows the exception in `src/Orders.Api/Pricing.cs:line 16`.
- `git blame src/Orders.Api/Pricing.cs` shows line 16 came from commit `9bfbf7d`.
- `git diff v1.3.0..v1.4.0` shows the only relevant pricing change is the new lookup-table approach and added silver tier.

Confidence
- High. The failure reproduces locally, the tests pinpoint the exact exception and line, and the regression aligns directly with the 1.4.0 pricing change.