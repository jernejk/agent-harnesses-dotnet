### Symptom
`GET /orders/1001/total` returns HTTP 500. The test suite also fails on standard-customer pricing.

### Reproduction
- `dotnet test`: 3 failed, 5 passed. Pricing tests throw `KeyNotFoundException` for key `standard`; the API integration test receives `InternalServerError`.
- Local `GET /orders/1001/total`: **500**.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. This commit is included between `v1.3.0` and `v1.4.0`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `Pricing.Total` directly indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 contains `gold` and `silver`, but not `standard`. Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws and the API responds with 500.

### Evidence
At `v1.3.0`, pricing used a conditional that assigned zero discount to any non-gold tier; the 1.4.0 lookup-table change removed that fallback. The failing pricing and API tests confirm the regression.

I couldn’t access `https://status.example.com` from this environment. No code changes were made; this was an investigation only.

### Confidence
**High** — the failing key, lookup site, affected order/customer, and introducing commit all align.