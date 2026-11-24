I have enough to name the regression and will summarize it for the engineer.
Symptom
- GET `/orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.
- The API health check is still OK, but the totals endpoint fails for normal orders/customers.

Reproduction
- `dotnet test` fails with:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `Orders.Tests.PricingTests.Totals_round_to_cents`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`
- Runtime error:
  - `System.Collections.Generic.KeyNotFoundException : The given key 'standard' was not present in the dictionary.`
  - It occurs in `Orders.Api.Pricing.Total(...)` at `src/Orders.Api/Pricing.cs:16`
- Local API check:
  - `GET /health -> 200`
  - version reported: `1.4.0`

Change that caused it (commit and subject)
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`
- This change replaced the old conditional logic with a dictionary lookup for tier discounts.

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- Code uses `TierDiscounts[customer.Tier]` directly.
- The lookup table only contains `gold` and `silver`, but existing customers use tier `standard`.
- Because `standard` is missing from the dictionary, the code throws `KeyNotFoundException`, which surfaces as HTTP 500 on the totals endpoint.

Evidence
- `git log --oneline v1.3.0..HEAD` shows the relevant release-side change:
  - `f1812c8 Release 1.4.0`
  - `ab38e53 Return problem details for unknown orders`
  - `9bfbf7d Move tier discounts into a lookup table and add silver tier`
- `git diff v1.3.0..HEAD -- src/Orders.Api/Pricing.cs` shows:
  - old logic: `customer.Tier == "gold" ? 0.05m : 0m`
  - new logic: `TierDiscounts[customer.Tier]`
- `git blame -L 1,30 src/Orders.Api/Pricing.cs` points line 16 to commit `9bfbf7d`
- Failing tests confirm the bug:
  - standard customer pricing tests fail immediately with missing key `standard`
  - API totals test returns `InternalServerError`

Confidence
- High. The failure reproduces locally, the exception message identifies the exact missing key, and the regression maps directly to the 1.4.0-era pricing change in `Pricing.cs:16`.