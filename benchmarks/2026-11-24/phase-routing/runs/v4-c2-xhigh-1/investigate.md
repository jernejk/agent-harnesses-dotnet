Status page: I can’t access `https://status.example.com` from this environment.

### Symptom
`GET /orders/1001/total` returns HTTP 500. This affects the standard-tier order; gold and silver order totals succeed.

### Reproduction
- `GET /orders/1001/total` → **500**
- `dotnet test` → **3 failed, 5 passed**; the failures include both standard-tier pricing tests and the API total test.
- `GET /orders/1002/total` and `/orders/1003/total` → **200**

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier.”**

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 contains only `gold` and `silver`, so looking up `standard` throws `KeyNotFoundException`, which surfaces as HTTP 500. The endpoint calls this pricing method at `src/Orders.Api/Program.cs:24`.

### Evidence
`src/Orders.Api/OrderStore.cs:14` assigns order 1001 to customer 2, whose tier is `standard` (`OrderStore.cs:8`). At `v1.3.0`, pricing instead used a conditional that applied the gold discount only to gold customers and gave other tiers a zero discount. The `v1.4.0` change removed that fallback. Git blame attributes the failing lookup to `9bfbf7d`.

No fix was made; this was kept to investigation as instructed.

### Confidence
**High** — the API response, failing tests, data mapping, current source, and pre-change implementation all agree.