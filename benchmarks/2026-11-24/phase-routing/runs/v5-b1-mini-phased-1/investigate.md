Symptom  
`GET /orders/{id}/total` returns HTTP 500 for valid orders after the 1.4.0 deploy.

Reproduction  
- `dotnet test` fails.
- `GET /orders/1001/total` returns `500`.
- Failing tests include:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `Orders.Tests.PricingTests.Totals_round_to_cents`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available`

Change that caused it (commit and subject)  
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)  
- `src/Orders.Api/Pricing.cs:16`
- The code now does `TierDiscounts[customer.Tier]`, but the dictionary only contains `"gold"` and `"silver"`.
- Existing customers have tier `"standard"` in the data/tests, so lookup throws `KeyNotFoundException`, which bubbles up as HTTP 500.

Evidence  
- Test output shows:
  - `System.Collections.Generic.KeyNotFoundException : The given key 'standard' was not present in the dictionary.`
  - Stack trace points to `Orders.Api.Pricing.Total(Order order, Customer customer) in /_/src/Orders.Api/Pricing.cs:line 16`
- API call:
  - `GET /orders/1001/total -> 500`
- Git history since `v1.3.0` includes:
  - `9bfbf7d Move tier discounts into a lookup table and add silver tier`
  - `f1812c8 Release 1.4.0`
- The relevant diff shows the unsafe change from:
  - `customer.Tier == "gold" ? 0.05m : 0m`
  - to `TierDiscounts[customer.Tier]`

Confidence  
High. The failing tests, API reproduction, stack trace, and recent code change all point to the same line and failure mode.