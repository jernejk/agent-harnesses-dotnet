### Symptom
`GET /orders/1001/total` returns HTTP 500. The failure is specific to the standard-tier customer; totals for orders 1002 and 1003 return 200.

### Reproduction
`dotnet test` failed 3 tests, including the API assertion for order 1001. A direct request to `/orders/1001/total` returned 500; requests for `/orders/1002/total` and `/orders/1003/total` returned 200.

### Change that caused it (commit and subject)
`9bfbf7d` — **“Move tier discounts into a lookup table and add silver tier”**. This commit replaced the previous non-gold zero-discount fallback with a dictionary lookup, but added entries only for `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` when the tier is `standard`, because the dictionary at lines 7–11 has no `standard` entry. Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`). The exception propagates from the total handler (`src/Orders.Api/Program.cs:23–24`) and produces the 500.

### Evidence
At v1.3.0, `Pricing.Total` used a zero-discount fallback for non-gold tiers. The new code’s missing key is also identified in the test failures for both standard-tier pricing tests and the order-1001 endpoint test. No files were changed during this investigation.

### Confidence
High.

I couldn’t access `https://status.example.com` from this environment, so I couldn’t check for an incident.