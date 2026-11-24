### Symptom
`GET /orders/1001/total` returns HTTP 500 on version 1.4.0. The service health endpoint reports version 1.4.0.

### Reproduction
`dotnet test` reproduced the issue: 3 tests failed and 5 passed. The failures include the endpoint test and two pricing tests, all reporting a missing `standard` key. A direct request to `/orders/1001/total` returned 500; requests for orders 1002 and 1003 returned 200.

I couldn’t access `https://status.example.com` from this environment, so I couldn’t check for an incident.

### Change that caused it (commit and subject)
`9bfbf7d8` — **Move tier discounts into a lookup table and add silver tier**. This change replaced the prior fallback that gave non-gold customers no discount with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The dictionary at lines 7–11 defines only `gold` and `silver`, so looking up `standard` throws `KeyNotFoundException`.

Order 1001 uses customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`). The total handler calls `Pricing.Total` without handling that exception (`src/Orders.Api/Program.cs:23–24`), resulting in the 500.

### Evidence
The test stack trace identifies `Pricing.Total` at `Pricing.cs:16` and reports the missing `standard` key. Git blame attributes that lookup line to `9bfbf7d8`; the v1.3.0 comparison shows it replaced the previous logic that treated non-gold tiers as having no discount. Gold and silver API requests succeed, consistent with the lookup containing those keys.

### Confidence
High. The exception, customer-tier data, failing tests, API behavior, and git diff all point to the same cause. No changes were made; this was an investigation only.