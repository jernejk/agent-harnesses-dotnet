### Symptom
`GET /orders/{id}/total` returns HTTP 500 for orders with a standard-tier customer.

### Reproduction
`GET /orders/1001/total` returned 500. `dotnet test` failed 3 of 8 tests: two pricing tests threw `KeyNotFoundException` for `"standard"`, and the API test got `InternalServerError` instead of `OK`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. It replaced the previous non-gold fallback with a dictionary lookup containing only `"gold"` and `"silver"`. The other post-1.3.0 commit, `ab38e53` (“Return problem details for unknown orders”), concerns unknown-order responses and is unrelated.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 has no `"standard"` entry. Order 1001 maps to customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws and the endpoint returns 500.

### Evidence
The existing standard-tier expectations remain in `tests/Orders.Tests/PricingTests.cs:14,26`; the endpoint calls `Pricing.Total` in `src/Orders.Api/Program.cs:24`. I couldn’t access the status page from this environment, so I couldn’t check for an incident. No code was changed during this investigation.

### Confidence
High — the reproduced exception, order/customer data, failing tests, and release diff all point to the same missing dictionary entry.