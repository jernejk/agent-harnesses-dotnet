### Symptom
`GET /orders/1001/total` returns HTTP 500. Order 1001 belongs to a `standard` customer.

### Reproduction
- `GET /orders/1001/total` returned 500.
- `dotnet test` failed 3 of 8 tests, including both standard-tier pricing tests and the API total test.
- Totals for orders 1002 (gold) and 1003 (silver) returned 200.
- I can’t access the external status page in this environment.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This change replaced the previous zero-discount fallback for non-gold customers with a dictionary lookup, but only added `gold` and `silver` entries.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. For `standard`, no key exists—the table only defines `gold` and `silver` at lines 9–10—so the lookup throws `KeyNotFoundException`. The endpoint calls this method at `src/Orders.Api/Program.cs:24`, resulting in HTTP 500.

### Evidence
`src/Orders.Api/OrderStore.cs:8` assigns customer 2 the `standard` tier, and order 1001 belongs to customer 2. The test failures report the missing `standard` key at `Pricing.cs:16`. No files were changed; a fix should preserve the prior zero-discount behavior for standard customers.

### Confidence
High: reproduction, test failures, source, and git attribution all point to the same missing lookup entry.