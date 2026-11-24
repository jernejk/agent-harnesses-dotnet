### Symptom
`GET /orders/1001/total` returns HTTP 500. Orders for gold and silver customers succeed.

### Reproduction
- `GET /orders/1001/total` → 500
- `GET /orders/1002/total` (gold) → 200
- `GET /orders/1003/total` (silver) → 200
- `dotnet test` → 3 failed, 5 passed. Failures include `KeyNotFoundException` for key `standard` and an API test expecting OK but receiving InternalServerError.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. The 1.4.0 release includes this change. It replaced the previous “gold discount, otherwise zero” logic with a lookup that has no entry for `standard`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The dictionary at lines 7–11 contains only `gold` and `silver`, so a standard customer causes `KeyNotFoundException`, which surfaces as HTTP 500.

### Evidence
`src/Orders.Api/OrderStore.cs:8` assigns customer 2 the `standard` tier, and order 1001 belongs to customer 2 (`OrderStore.cs:14`). Existing pricing tests also exercise the standard tier. No files were changed. I couldn’t access the external status page from this environment.

### Confidence
High — the reproduced API behavior, test exception, seeded data, and diff all point to the same missing standard-tier dictionary entry.