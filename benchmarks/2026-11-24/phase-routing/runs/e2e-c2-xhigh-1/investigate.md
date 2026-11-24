### Symptom
`GET /orders/1001/total` returns HTTP 500. The failure is specific to the standard-tier customer; gold and silver totals succeed.

### Reproduction
- `GET /orders/1001/total` → **500**
- `dotnet test` → **3 failed, 5 passed**. Failures include the API test and two pricing tests; the exception is `KeyNotFoundException` for key `standard`.
- Control requests: `/orders/1002/total` and `/orders/1003/total` → **200**.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. This change introduced the tier-discount dictionary and replaced the previous gold-or-no-discount logic with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`. Order 1001 maps to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`). That lookup throws `KeyNotFoundException`; the endpoint invokes it at `src/Orders.Api/Program.cs:24`, resulting in HTTP 500.

### Evidence
- `git blame` attributes the dictionary and lookup lines to `9bfbf7d`.
- The test output shows `KeyNotFoundException` for `standard` at `Pricing.cs:16`.
- The old pricing logic returned a zero discount for non-gold tiers; the new dictionary lookup has no `standard` entry.
- I did not change any code; this was an investigation only.
- I can’t access the status page from this environment.

### Confidence
High. The exception, affected tier, source data, endpoint call, and regression-introducing commit all align.