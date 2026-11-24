### Symptom
`GET /orders/1001/total` returns HTTP 500. I couldn’t open the status page from this environment, so I couldn’t check for an incident.

### Reproduction
The local request returned 500. `dotnet test` also failed: 3 failed, 5 passed. The failures include `KeyNotFoundException` for key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. It replaced the prior non-gold fallback with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the table at lines 7–11 contains only `gold` and `silver`. Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws and the API returns 500. In v1.3.0, non-gold tiers received no discount instead of throwing.

### Evidence
The API reproduced the 500; the tests show the missing `standard` key and a stack trace pointing to `Pricing.Total` line 16. `git blame` attributes that lookup line to `9bfbf7d`.

### Confidence
High.