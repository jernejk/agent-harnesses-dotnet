### Symptom
`GET /orders/1001/total` returns HTTP 500. The API health check reports version 1.4.0.

### Reproduction
- `GET /orders/1001/total` → **500**
- `dotnet test` → **3 failed, 5 passed**; failures include both standard-tier pricing tests and the API total test.
- Control requests for orders 1002 (gold) and 1003 (silver) return **200**.
- I couldn’t access `https://status.example.com` from this environment.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit replaced the prior standard-tier fallback with a dictionary lookup but did not add a `standard` entry.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` by `customer.Tier`. The dictionary at lines 7–11 includes only `gold` and `silver`, so a `standard` customer causes `KeyNotFoundException`, which becomes the 500 response. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`).

### Evidence
At v1.3.0, `Pricing.Total` assigned zero discount to any tier other than gold. The 1.4.0 change made the lookup strict. The test failures and successful gold/silver endpoint requests match that regression.

### Confidence
High. **No files changed**; this was investigation only.