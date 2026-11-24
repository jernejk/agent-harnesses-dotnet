### Symptom
GET `/orders/1001/total` returns HTTP 500 in the 1.4.0 deployment. I can’t open `https://status.example.com` from this environment.

### Reproduction
`dotnet test` failed 3 of 8 tests; the failures report `KeyNotFoundException` for key `standard`. GET `/orders/1001/total` returned 500. The API reports version 1.4.0; totals for orders 1002 and 1003 succeeded.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with the customer’s tier. That dictionary contains `gold` and `silver` (lines 9–10), but not `standard`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`), so pricing throws `KeyNotFoundException`, resulting in HTTP 500.

### Evidence
In v1.3.0, `Pricing.Total` gave every non-gold tier a zero discount. The 1.4.0 change replaced that fallback with dictionary lookup. The test failures and successful silver/gold-backed API requests match this cause.

### Confidence
High. No files were changed; this was investigation only.