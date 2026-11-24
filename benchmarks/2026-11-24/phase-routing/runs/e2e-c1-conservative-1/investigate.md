### Symptom
`GET /orders/1001/total` returns HTTP 500.

### Reproduction
`dotnet test` failed 3 of 8 tests. The failures include `OrdersApiTests.Order_totals_are_available` (expected OK, got InternalServerError) and pricing tests throwing `KeyNotFoundException` for key `"standard"`. A direct GET to `/orders/1001/total` also returned 500.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **Move tier discounts into a lookup table and add silver tier**. This change is included between `v1.3.0` and `v1.4.0`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table at lines 7–11 contains `"gold"` and `"silver"`, but not `"standard"`, so standard-tier orders throw `KeyNotFoundException`. `/orders/1001` maps to a standard-tier customer in `src/Orders.Api/OrderStore.cs:9,14`.

### Evidence
The API handler calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`; the test stack trace points to the lookup at `Pricing.cs:16`. The status page could not be accessed from this environment. No files were changed.

### Confidence
High. The reproduction, stack trace, customer data, and release diff all point to the same missing tier entry.