### Symptom
`GET /orders/1001/total` returns HTTP 500. The failure affects the standard-tier order; gold and silver orders succeed.

### Reproduction
`dotnet test` failed 3 of 8 tests, including the standard-customer pricing tests and the API total test. The API returned 500 for order 1001, while orders 1002 and 1003 returned 200.

Status page: I couldn’t access `https://status.example.com` with the available tools.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The map at lines 7–11 contains `gold` and `silver` but not `standard`; looking up `standard` throws `KeyNotFoundException`, which causes the endpoint’s 500. Before this change, non-gold customers received no discount.

### Evidence
The test stack trace points to `Pricing.Total` at line 16. The tests include a `standard` customer, and order 1001 belongs to a standard-tier customer; gold and silver endpoint requests succeed. No code changes were made during this investigation.

### Confidence
High.