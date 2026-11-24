### Symptom
`GET /orders/1001/total` returns HTTP 500. The failure affects orders whose customer tier is `standard`.

### Reproduction
- `dotnet test`: 3 failed, 5 passed. The failures include two pricing tests and the API total test; each reports a missing `standard` key.
- `GET /orders/1001/total` returned 500. Gold and silver order totals returned 200.
- I couldn’t access `https://status.example.com` from this environment.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. This change replaced the previous fallback for non-gold customers with a lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer tier, but that dictionary contains only `gold` and `silver` (`Pricing.cs:7–11`). Order 1001’s customer is `standard` (`src/Orders.Api/OrderStore.cs:8`), so the lookup throws `KeyNotFoundException`, producing the 500.

### Evidence
`git blame` attributes the failing lookup at `Pricing.cs:16` to `9bfbf7d`. The test stack traces point to the same line. The endpoint passes for orders with gold and silver customers.

### Confidence
High. The failing request, test errors, current data, and commit history all point to the same cause. No code changes were made; this was an investigation only.