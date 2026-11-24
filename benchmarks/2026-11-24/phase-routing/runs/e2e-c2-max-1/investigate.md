### Symptom
`GET /orders/1001/total` returns HTTP 500. Order 1001 belongs to a standard-tier customer.

### Reproduction
- `dotnet test`: 3 failed, 5 passed. The failures include two pricing tests and the API test for order 1001.
- Local API: order 1001 returns 500; orders 1002 (gold) and 1003 (silver) return 200.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**, included between v1.3.0 and v1.4.0.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:7–11` defines discounts for `gold` and `silver`, but not `standard`. At `Pricing.cs:16`, `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for standard-tier customers. The total endpoint calls this code at `src/Orders.Api/Program.cs:24`; `src/Orders.Api/OrderStore.cs:8,14` maps order 1001 to a standard-tier customer.

### Evidence
- The test exception identifies the missing `"standard"` key at `Pricing.cs:16`.
- At v1.3.0, pricing treated every non-gold tier as having no discount; the v1.4.0 change replaced that fallback with the incomplete dictionary lookup.
- I could not access `https://status.example.com` from this environment, so I could not verify an incident.
- No files were changed; this was investigation only.

### Confidence
High — the failing request, test exception, affected customer record, and regression commit all point to the missing standard-tier entry.