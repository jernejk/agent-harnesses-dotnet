### Symptom
`GET /orders/1001/total` returns HTTP 500 on the local API, which reports version `1.4.0`.

### Reproduction
- `GET /orders/1001/total` → 500.
- `dotnet test` → 3 failed, 5 passed. The failures include two pricing tests and the endpoint test; all report a missing `standard` dictionary key.
- Control requests for gold order `1002` and silver order `1003` both return 200.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This change replaced the prior logic—which gave non-gold customers no discount—with a direct lookup containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for `standard`, because that tier has no entry in the lookup. Order `1001` is assigned to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`).

### Evidence
- `Pricing.cs:7–11` defines entries for `gold` and `silver` only; `Pricing.cs:16` indexes by the customer’s tier.
- The test stack trace points to `Pricing.Total` at line 16. Git blame attributes that lookup to commit `9bfbf7d`.
- Gold and silver endpoint requests succeed, isolating the failure to the standard tier.

### Confidence
High. The reproduced 500, test failures, seeded data, and release diff all point to the same cause.

I couldn’t access the status page from this environment. No code was changed.