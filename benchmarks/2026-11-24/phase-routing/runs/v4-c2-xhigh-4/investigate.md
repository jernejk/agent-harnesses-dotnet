### Symptom
`GET /orders/1001/total` returns HTTP 500. I can’t open `status.example.com` from this environment, so I couldn’t check its incident status.

### Reproduction
- Local API: `/orders/1001/total` returned 500; `/orders/1002/total` and `/orders/1003/total` returned 200.
- `dotnet test`: 3 failures, all due to `KeyNotFoundException` for key `'standard'` or the related expected-OK/actual-500 assertion.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit is between `v1.3.0` and `v1.4.0`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` directly indexes a dictionary containing only `"gold"` and `"silver"` (lines 7–11). Order 1001 belongs to customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws and the API returns 500.

### Evidence
Before the change, pricing assigned a 5% discount to gold and 0% to every other tier. The commit replaced that fallback with a direct dictionary lookup without adding a `"standard"` entry. Gold and silver order totals still succeed.

### Confidence
High. The failing key, exception location, changed pricing logic, and order/customer mapping all agree. No code changes were made.