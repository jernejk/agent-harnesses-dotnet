The release introduced silver tier data without a standard default in pricing, which matches the 500.
Symptom  
- `GET /orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.
- Repro on the local API: `GET /orders/1001/total -> 500`.

Reproduction  
- Ran `dotnet test`; the API test `Orders.Tests.OrdersApiTests.Order_totals_are_available` fails with `Expected: OK / Actual: InternalServerError`.
- The pricing unit tests fail with `KeyNotFoundException: The given key 'standard' was not present in the dictionary.`

Change that caused it (commit and subject)  
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)  
- `src/Orders.Api/Pricing.cs:16`
- `var discount = TierDiscounts[customer.Tier];` now assumes every customer tier exists in the dictionary.
- The dictionary only contains `gold` and `silver`, but existing customers still have tier `standard` in `src/Orders.Api/OrderStore.cs` and tests. That missing key throws `KeyNotFoundException`, which becomes HTTP 500 for total requests.

Evidence  
- `git diff v1.3.0..HEAD -- src/Orders.Api/Pricing.cs` shows the regression from:
  - old: `customer.Tier == "gold" ? 0.05m : 0m`
  - new: dictionary lookup with no fallback
- `git diff v1.3.0..HEAD -- src/Orders.Api/OrderStore.cs` shows customers still using `standard`.
- `git show 9bfbf7d:tests/Orders.Tests/PricingTests.cs` includes a `standard` test case, confirming the code path must support it.
- `dotnet test` stack trace points directly to `Orders.Api.Pricing.Total(...)` line 16.

Confidence  
- High. The failure reproduces consistently, the stack trace points to a single line, and the release diff shows the exact change that introduced the bad assumption.