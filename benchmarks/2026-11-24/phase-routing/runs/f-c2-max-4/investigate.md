### Symptom
`GET /orders/1001/total` returns HTTP 500 in 1.4.0. The order itself is retrievable, and `/health` returns 200.

### Reproduction
- `dotnet test`: 3 failures, 5 passes. The failures include both standard-tier pricing tests and the API total test; the exception is `KeyNotFoundException` for `standard`.
- `GET /orders/1001/total`: 500. Gold and silver orders (`1002` and `1003`) return 200.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. The v1.3.0-to-v1.4.0 diff shows this change replaced the prior default-zero-discount logic with a dictionary lookup. The other post-v1.3.0 commits concern unknown-order responses and the 1.4.0 release.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:7–11,16`: the discount table has entries for `gold` and `silver`, but not `standard`; line 16 indexes it directly with `customer.Tier`, throwing instead of applying the previous zero discount. `Program.cs:23–24` passes the order’s stored customer into this calculation.

### Evidence
The stored customer for order 1001 is `standard` (`src/Orders.Api/OrderStore.cs:8,14`). The test stack trace points to `Pricing.cs:16`; the API reproduces the 500, while gold and silver totals succeed.

Status page: I couldn’t access https://status.example.com because internet access is unavailable, so I couldn’t verify an incident.

### Confidence
High. No code changes were made; this was a read-only investigation.