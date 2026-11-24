### Symptom
`GET /orders/1001/total` returns HTTP 500. The health endpoint reports version `1.4.0`.

### Reproduction
- `GET /orders/1001/total` returned 500.
- `dotnet test` failed 3 of 8 tests; failures include two pricing tests and the API total test.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit is in the changes since `v1.3.0`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer tier, but the table at lines 7–11 contains only `gold` and `silver`, not `standard`. Order `1001` belongs to a `standard` customer (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException`, resulting in the 500.

### Evidence
The test output shows `KeyNotFoundException` for key `standard` at `Pricing.cs:16`; the API probe returned 500. No files were changed. I can’t access `https://status.example.com` from this environment, so I could not check for an incident.

### Confidence
High. The API failure, test failures, source, and blame all point to the same regression.