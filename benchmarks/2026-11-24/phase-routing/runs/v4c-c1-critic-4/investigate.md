### Symptom
`GET /orders/1001/total` returns HTTP 500. Gold- and silver-tier order totals succeed. I can’t access `https://status.example.com` from this environment, so I couldn’t check for an incident.

### Reproduction
`dotnet test` failed 3 of 8 tests, including the endpoint test; pricing tests threw `KeyNotFoundException` for key `standard`. The local API returned 500 for order 1001, and 200 for orders 1002 and 1003.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. It changed pricing from a gold-specific discount with a zero-discount fallback to direct lookup in a table containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 has no `standard` entry, so pricing throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8`); the endpoint calls pricing at `src/Orders.Api/Program.cs:24`, resulting in the 500.

### Evidence
The standard-tier pricing tests still expect list price plus GST (`tests/Orders.Tests/PricingTests.cs:13–14, 25–26`). Git diff from `v1.3.0` shows the direct lookup introduced in `9bfbf7d`; the other tiers’ endpoint requests succeeded. No files were changed.

### Confidence
High. The exception, missing dictionary key, failing test expectations, order data, and endpoint result all point to the same cause.