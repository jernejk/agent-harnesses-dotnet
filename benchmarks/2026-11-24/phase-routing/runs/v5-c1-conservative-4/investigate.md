### Symptom
`GET /orders/1001/total` returns HTTP 500 since v1.4.0. The health endpoint is healthy and reports version 1.4.0.

### Reproduction
`dotnet test` failed 3 of 8 tests. The failures include the API total test and pricing tests; each reports a missing `standard` key. A direct `GET /orders/1001/total` also returned HTTP 500.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. This changed pricing from treating non-gold tiers as zero discount to indexing a tier-discount dictionary.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts[customer.Tier]`, but the dictionary at lines 7–11 contains only `gold` and `silver`, not `standard`. Order 1001 maps to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8`); the lookup throws `KeyNotFoundException`, which produces the 500.

### Evidence
The v1.3.0 diff shows the prior fallback gave all non-gold customers a zero discount. The test output and API response reproduce the failure; `GET /health` returns 200. No files were changed.

Status page: I can’t access `https://status.example.com` from this environment.

### Confidence
High.