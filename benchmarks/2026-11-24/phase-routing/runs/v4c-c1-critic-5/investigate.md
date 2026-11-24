### Symptom
`GET /orders/1001/total` returns HTTP 500 since 1.4.0. This affects standard-tier orders; gold and silver orders succeed.

### Reproduction
- `GET /orders/1001/total` → 500.
- `dotnet test` → 3 failed, 5 passed. The failures include two pricing tests and the API test; the exceptions report a missing `'standard'` key.
- Control requests: `/orders/1002/total` and `/orders/1003/total` → 200.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This change replaced the fallback for non-gold customers with a direct lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 contains only `gold` and `silver`, but order 1001 belongs to a `standard` customer (`OrderStore.cs`), so the lookup throws `KeyNotFoundException`. The exception occurs in the `/orders/{id:int}/total` handler at `src/Orders.Api/Program.cs:24`, resulting in the 500.

### Evidence
The `v1.3.0..HEAD` history identifies `9bfbf7d` as the pricing change; `git blame` attributes the direct lookup at line 16 to that commit. No files were changed during this investigation.

Status page: I couldn’t access it from this environment because internet access is unavailable.

### Confidence
High. The failing request, test exceptions, tier data, and changed lookup all agree.