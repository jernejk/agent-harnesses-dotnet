### Symptom
`GET /orders/1001/total` returns HTTP 500. Order 1001 belongs to a `standard` customer.

### Reproduction
`GET /orders/1001/total` returned 500. `dotnet test` failed 3 of 8 tests: the two standard-tier pricing tests and the API totals test for order 1001. The exception was `KeyNotFoundException` for key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit is between v1.3.0 and v1.4.0.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 contains only `gold` and `silver`. For a standard customer, the lookup throws instead of applying the previous zero-discount fallback; the exception becomes HTTP 500.

### Evidence
The `9bfbf7d` diff replaces the prior gold-or-zero conditional with the dictionary lookup and adds the silver entry, but no standard entry. `src/Orders.Api/OrderStore.cs` assigns order 1001 to customer 2, whose tier is `standard`. Tests and the API probe reproduce the failure. No code was changed.

Status page: I couldn’t access `https://status.example.com` from this environment.

### Confidence
High. The failing key, runtime exception, changed lookup, and affected order’s tier all agree.