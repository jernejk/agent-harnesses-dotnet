### Symptom
`GET /orders/1001/total` returns HTTP 500. The endpoint fails for the standard-tier order; gold and silver orders return 200.

### Reproduction
`dotnet test` produced 3 failures and 5 passes. The failing pricing tests and API test report `KeyNotFoundException` for key `standard`. Direct API calls confirmed `/orders/1001/total` returns 500, while `/orders/1002/total` and `/orders/1003/total` return 200.

### Change that caused it (commit and subject)
`9bfbf7d8` — **Move tier discounts into a lookup table and add silver tier**. It changed pricing from a fallback that charged standard customers no discount to direct dictionary lookup. This commit is in the history leading to the `f1812c8` **Release 1.4.0** commit.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` indexes a dictionary containing `gold` and `silver`, but no `standard`. Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`). The lookup throws `KeyNotFoundException`, which results in the 500.

### Evidence
The v1.3.0 implementation treated every non-gold tier as having no discount. Git blame attributes the new lookup at line 16 to `9bfbf7d8`. The status page could not be checked because internet access is unavailable.

### Confidence
High. The API response, failing tests, source, and Git history all point to the same cause. No code was changed.