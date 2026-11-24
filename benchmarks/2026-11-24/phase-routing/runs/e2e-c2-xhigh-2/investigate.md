### Symptom
`GET /orders/1001/total` returns HTTP 500.

### Reproduction
- `GET /orders/1001/total` returned 500.
- `dotnet test` failed 3 of 8 tests, including the standard-customer pricing tests and the API totals test.

### Change that caused it (commit and subject)
`9bfbf7d8` — **“Move tier discounts into a lookup table and add silver tier”**. It replaced the previous zero-discount fallback for non-gold customers with a dictionary lookup containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` when the tier is `standard`, because that key is absent from the table. Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`). The exception reaches the endpoint and produces the 500.

### Evidence
The v1.3.0 implementation treated every non-gold tier as having no discount. Git blame attributes the failing lookup to `9bfbf7d8`. I can’t access `https://status.example.com` in this environment.

### Confidence
High. The API response, test failures, changed code, and affected order’s tier all point to the same cause. No code was changed; this investigation was limited to diagnosis.