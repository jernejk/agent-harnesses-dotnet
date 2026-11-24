### Symptom
`GET /orders/1001/total` returns HTTP 500.

### Reproduction
The local API reproduced the 500. `dotnet test` failed 3 of 8 tests; failures included `KeyNotFoundException` for key `standard` and the API test expecting `OK` but receiving `InternalServerError`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `Pricing.Total` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`, not `standard`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8`), so the lookup throws. The endpoint calls this method at `src/Orders.Api/Program.cs:24`.

### Evidence
The diff from v1.3.0 shows the prior fallback treated non-gold customers as having no discount; the new direct dictionary lookup has no `standard` entry. I couldn’t open the status page from this environment. No code was changed.

### Confidence
High. The reproduced exception, failing tests, current source, and release diff all point to the same cause.