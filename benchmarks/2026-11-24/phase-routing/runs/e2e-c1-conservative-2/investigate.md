### Symptom
`GET /orders/1001/total` returns HTTP 500. Order 1001 belongs to customer 2, whose tier is `standard`.

### Reproduction
`dotnet test` failed: 3 tests failed and 5 passed. The pricing tests and API test report a `KeyNotFoundException` for key `standard`. The local API returned 500 for `/orders/1001/total`; `/orders/1003/total` returned 200.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. The commit replaced the previous non-gold fallback with a tier-discount lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` indexes the dictionary directly, but the dictionary at lines 7–11 contains only `gold` and `silver`, not `standard`. For order 1001, that lookup throws and produces the 500.

### Evidence
`src/Orders.Api/OrderStore.cs:8` assigns customer 2 the `standard` tier, and line 14 links order 1001 to customer 2. `src/Orders.Api/Program.cs:23–24` passes that customer to `Pricing.Total`. In v1.3.0, pricing used a zero-discount fallback for every non-gold tier, so `standard` did not throw. **The status page could not be opened from this environment.**

### Confidence
High: the endpoint response, failing tests, data, and v1.3.0-to-v1.4.0 code diff all point to the missing `standard` mapping. No code was changed.