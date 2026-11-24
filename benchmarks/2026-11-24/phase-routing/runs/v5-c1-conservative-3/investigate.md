### Symptom
`GET /orders/1001/total` returns HTTP 500 on v1.4.0. The health endpoint reports version `1.4.0`.

### Reproduction
- `GET /orders/1001/total` → 500.
- `dotnet test` → 3 failed, 5 passed; failures include `KeyNotFoundException` for key `standard`.
- The `gold` order 1002 and `silver` order 1003 totals return 200.
- I can’t open the external status page in this environment.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This change replaced the prior standard-tier fallback with a dictionary lookup containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 has no `standard` entry. Order 1001 maps to a `standard` customer (`src/Orders.Api/OrderStore.cs:8,14`), so that lookup throws `KeyNotFoundException` and the endpoint returns 500.

### Evidence
The API and tests reproduce the same failure. The pricing tests for standard customers fail, while the gold and silver endpoint checks succeed. The v1.3.0-to-regression-commit diff shows the previous logic treated non-gold tiers as receiving no discount; the new lookup removed that behavior.

### Confidence
High. The exception, affected tier, source line, and release diff all match the observed failure.