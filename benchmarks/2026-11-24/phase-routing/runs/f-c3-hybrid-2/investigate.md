### Symptom
`GET /orders/1001/total` returns HTTP 500 after v1.4.0. Orders 1002 and 1003 return 200.

### Reproduction
`dotnet test` fails 3 of 8 tests. The pricing tests throw `KeyNotFoundException` for key `standard`, and the API test for order 1001 receives `InternalServerError`. Direct GETs confirmed order 1001 returns 500 while 1002 and 1003 return 200.

### Change that caused it (commit and subject)
`9bfbf7d8` — **Move tier discounts into a lookup table and add silver tier**. The change replaced the prior gold-or-no-discount fallback with a lookup containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts[customer.Tier]`. Order 1001 belongs to a `standard` customer, but the table at lines 7–11 has no `standard` entry. The resulting `KeyNotFoundException` is unhandled in the total route, producing HTTP 500.

### Evidence
The store assigns order 1001 to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs`). The v1.3.0 diff shows the previous code treated non-gold tiers as having no discount. **I can’t access the status page from this environment.** No code was changed.

### Confidence
High: the failing request, test exception, source, and commit diff all point to the same cause.