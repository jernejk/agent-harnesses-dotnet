Symptom
- `GET /orders/{id}/total` returns HTTP 500 for standard customers after the 1.4.0 deploy.
- Repro: `GET /orders/1001/total` returns `500`, and `dotnet test` fails with `KeyNotFoundException: The given key 'standard' was not present in the dictionary.`

Reproduction
- API call: `GET /orders/1001/total -> 500`
- Test run: `dotnet test`
- Failing tests:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `Orders.Tests.PricingTests.Totals_round_to_cents`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available`

Change that caused it (commit and subject)
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- The code now does `var discount = TierDiscounts[customer.Tier];`, but `TierDiscounts` only contains `"gold"` and `"silver"`.
- Customer `Noah Patel` in `src/Orders.Api/OrderStore.cs:8` is still tier `"standard"`, so looking up `TierDiscounts["standard"]` throws `KeyNotFoundException`, which bubbles up as HTTP 500.

Evidence
- `git diff v1.3.0..HEAD -- src/Orders.Api/Pricing.cs` shows the old ternary logic was replaced with a dictionary lookup.
- `git blame src/Orders.Api/Pricing.cs` attributes the lookup-table change to commit `9bfbf7d`.
- `src/Orders.Api/OrderStore.cs` shows an existing `"standard"` customer.
- `dotnet test` stack trace points directly to `Orders.Api.Pricing.Total(...)` line 16.

Confidence
- High. The failing request, failing tests, stack trace, and source diff all point to the same unhandled lookup for the `"standard"` tier.