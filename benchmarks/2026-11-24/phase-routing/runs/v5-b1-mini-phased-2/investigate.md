Symptom
- GET `/orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.
- Reproducible locally and in tests: `Orders.Tests.OrdersApiTests.Order_totals_are_available` fails with `InternalServerError`.

Reproduction
- `dotnet test` fails.
- `http_get /orders/1001/total` returns 500.
- Failing stack trace shows `System.Collections.Generic.KeyNotFoundException: The given key 'standard' was not present in the dictionary.` from `Orders.Api.Pricing.Total(...)`.

Change that caused it (commit and subject)
- `9bfbf7d Move tier discounts into a lookup table and add silver tier`
- This is the regression introduced between `v1.3.0` and `v1.4.0`.

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- The code changed from a safe conditional:
  - `customer.Tier == "gold" ? 0.05m : 0m`
- to an indexed lookup:
  - `var discount = TierDiscounts[customer.Tier];`
- The lookup table only contains `"gold"` and `"silver"`, but existing customers still use `"standard"`, so calling `/orders/{id}/total` for a standard customer throws `KeyNotFoundException`, which becomes HTTP 500.

Evidence
- Test output:
  - `KeyNotFoundException ... key 'standard' was not present in the dictionary`
  - stack trace points to `src/Orders.Api/Pricing.cs:16`
- `git diff v1.3.0..f1812c8 -- src/Orders.Api/Pricing.cs` shows the exact change.
- `tests/Orders.Tests/PricingTests.cs` expects standard customers to pay list price plus GST.
- `src/Orders.Api/Program.cs:23-24` calls `Pricing.Total(order, customer)` directly on the `/orders/{id}/total` path, so the exception becomes a 500.

Confidence
- High. The failing test, HTTP reproduction, and diff all point to the same line and missing `"standard"` tier handling.