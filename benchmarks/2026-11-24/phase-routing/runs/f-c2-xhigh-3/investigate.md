### Symptom
`GET /orders/1001/total` returns HTTP 500. The local health endpoint reports version 1.4.0.

### Reproduction
`dotnet test` fails 3 of 8 tests, including the total endpoint test for order 1001. A direct GET for order 1001 returns 500; orders 1002 and 1003 return 200.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. This commit is between v1.3.0 and v1.4.0.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` by customer tier. The table at lines 7–11 has `gold` and `silver`, but no `standard`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:14`), so the lookup throws `KeyNotFoundException`, which results in HTTP 500. Before the change, standard customers received a zero discount.

### Evidence
The failing tests report the missing `standard` key at `Pricing.cs:16`. The v1.3.0 implementation used a conditional that applied zero discount to non-gold tiers. I couldn’t access the external status page from this environment. No files were changed.

### Confidence
High: the failing test, API response, order/customer data, and commit diff all point to the same cause.