### Symptom
`GET /orders/1001/total` returns HTTP 500. The order belongs to a `standard` customer. A gold-customer order (`1002`) returns HTTP 200.

### Reproduction
- `GET /orders/1001/total` → 500.
- `dotnet test` → 3 failed, 5 passed. Failures include the API test and pricing tests; the exception is `KeyNotFoundException` for key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit replaced the previous “gold gets a discount; other tiers get zero” logic with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The dictionary at lines 7–11 contains only `gold` and `silver`, so the `standard` tier used by order `1001` throws `KeyNotFoundException`, resulting in HTTP 500.

### Evidence
`src/Orders.Api/OrderStore.cs` assigns order `1001` to customer `2`, whose tier is `standard`. In `v1.3.0`, `Pricing.Total` treated every non-gold tier as having no discount, so `standard` did not fail. `git blame` attributes the failing lookup to `9bfbf7d`.

I couldn’t open the external status page in this environment. No fix was made; this was investigation-only.

### Confidence
High — reproduced through both the API and tests, and the exception matches the changed lookup exactly.