Symptom  
GET `/orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy. The failing request I reproduced was `GET /orders/1001/total -> 500`.

Reproduction  
- `dotnet test` fails
- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
- `Orders.Tests.PricingTests.Totals_round_to_cents`
- `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`

The test failure is a `KeyNotFoundException` for tier `standard`.

Change that caused it (commit and subject)  
- `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0`
- `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)  
- `src/Orders.Api/Pricing.cs:16`
- `var discount = TierDiscounts[customer.Tier];`

Why: the new lookup table only contains `gold` and `silver`, but existing customers have tier `standard`. Indexing the dictionary with `standard` throws `KeyNotFoundException`, which bubbles up as HTTP 500 in the API.

Evidence  
- `git diff v1.3.0..HEAD -- src/Orders.Api/Pricing.cs` shows the old logic was:
  - `customer.Tier == "gold" ? 0.05m : 0m`
- The 1.4.0 change replaced that with a direct dictionary lookup.
- `git blame -L 1,40 src/Orders.Api/Pricing.cs` attributes line 16 to commit `9bfbf7d`.
- `dotnet test` stack trace points directly to `Pricing.Total(...)` line 16.
- Local API call to `/orders/1001/total` returns 500.

Confidence  
High. The failing tests, stack trace, git diff, and blame all point to the same line and the same regression commit.