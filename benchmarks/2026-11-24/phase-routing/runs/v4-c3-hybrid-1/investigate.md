### Symptom
`GET /orders/1001/total` returns HTTP 500. Order 1001 belongs to a `standard`-tier customer.

### Reproduction
`dotnet test` reproduced three failures and five passes. The failing API test is for order 1001; direct requests for orders 1002 and 1003 returned 200.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This change replaced the prior non-gold default discount with a dictionary lookup. The `f1812c8` release commit only adds the 1.4.0 version and not-found response changes.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with the customer’s tier. The table at lines 7–11 contains `gold` and `silver`, but not `standard`; that lookup throws `KeyNotFoundException`, resulting in HTTP 500. `src/Orders.Api/OrderStore.cs:8,14` maps order 1001 to a standard-tier customer.

### Evidence
The test output shows `KeyNotFoundException` for key `'standard'` at `Pricing.cs:16`; git blame attributes that lookup to `9bfbf7d`. Gold and silver orders return 200, while the standard-tier order fails. **I can’t access the status page from this environment.**

### Confidence
High. The failing request, test stack trace, customer mapping, and commit diff all point to the missing `standard` entry. No code was changed.