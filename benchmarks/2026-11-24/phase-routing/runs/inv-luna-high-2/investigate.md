### Symptom
Three tests fail: two pricing tests throw `KeyNotFoundException` for key `standard`, and the API test for order 1001 receives `500 Internal Server Error`.

### Reproduction
`dotnet test` reproduces all three failures. `GET /orders/1001/total` returns 500; `GET /orders/1001` succeeds and shows customer ID 2.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**, after v1.3.0. It replaced the prior standard-tier fallback with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws when the tier is absent. The new table at lines 7–11 defines `gold` and `silver`, but omits `standard`. Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`).

### Evidence
The test stack traces point to `Pricing.Total` at line 16 and report the missing `standard` key. The unchanged standard-tier tests expect list price plus GST, confirming standard customers remain supported.

### Confidence
High. The failing key, lookup, and affected order/customer mapping all match the reproduced failure.