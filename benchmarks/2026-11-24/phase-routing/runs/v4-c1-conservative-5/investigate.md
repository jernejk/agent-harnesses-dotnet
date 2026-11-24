### Symptom
`GET /orders/1001/total` returns HTTP 500. The status page could not be opened from this environment.

### Reproduction
- Local API: `/orders/1001/total` → 500.
- `dotnet test` → 3 failures, 5 passes. The pricing tests and the API test for order 1001 fail with a missing-key exception / unexpected `InternalServerError`.
- Totals for orders 1002 and 1003 return 200.

### Change that caused it (commit and subject)
`9bfbf7d8` — **Move tier discounts into a lookup table and add silver tier**. It replaced the prior “gold discount, otherwise no discount” logic with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with the customer’s tier. The table at lines 7–11 contains `"gold"` and `"silver"`, but not `"standard"`. Order 1001 belongs to customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException`, causing the endpoint to return 500.

### Evidence
`git diff v1.3.0..v1.4.0` identifies the pricing change; `git blame` attributes the lookup at line 16 to `9bfbf7d8`. The existing standard-tier tests expect pricing to succeed, and they fail with the same missing-key exception.

### Confidence
High. The failure is reproduced and the missing key is directly confirmed in the code. No files were changed.