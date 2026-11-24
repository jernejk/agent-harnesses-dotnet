### Symptom
`GET /orders/1001/total` returns HTTP 500. The endpoint works for the gold and silver orders.

### Reproduction
`dotnet test` failed: 3 failed, 5 passed. The failures include the standard-tier pricing tests and the API test for order 1001. Direct requests returned 500 for order 1001, but 200 for orders 1002 and 1003.

### Change that caused it (commit and subject)
`9bfbf7d8` — **Move tier discounts into a lookup table and add silver tier**. It was included in the `v1.4.0` release. The change replaced the previous default-zero discount for non-gold customers with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:7–11,16`: the `TierDiscounts` dictionary has entries for `gold` and `silver`, but not `standard`; line 16 indexes it using `customer.Tier`, which throws `KeyNotFoundException` for standard-tier customers.

Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`). The endpoint passes that customer to `Pricing.Total` (`src/Orders.Api/Program.cs:23–24`), so the exception produces the 500.

### Evidence
Git blame attributes the incomplete lookup and indexed access to `9bfbf7d`. The test output shows the missing-`standard`-key exception at `Pricing.cs:16`. The status page could not be opened from this environment, so I can’t confirm whether it reports an incident. No code was changed.

### Confidence
High. The endpoint response, failing tests, source, and commit history all point to the same cause.