### Symptom
Order total calculation fails for standard-tier customers. The API returns HTTP 500 for order `1001`.

### Reproduction
`dotnet test` reproduced 3 failures out of 8 tests. Two pricing tests throw `KeyNotFoundException` for key `standard`; the API test for order `1001` fails because it receives `InternalServerError` instead of `OK`. A direct `GET /orders/1001/total` also returned 500.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit replaced the prior “gold gets 5%, otherwise 0%” logic with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` indexes the dictionary directly, but the table at lines 7–11 contains only `gold` and `silver`. For a `standard` customer, the lookup throws `KeyNotFoundException`.

### Evidence
- `src/Orders.Api/OrderStore.cs` assigns order `1001` to customer `2`, whose tier is `standard`.
- `tests/Orders.Tests/PricingTests.cs` expects standard customers to pay list price plus GST.
- At v1.3.0, `Pricing.Total` applied a 5% discount only to gold customers and used zero discount otherwise; commit `9bfbf7d` changed that behavior to the incomplete lookup.
- The test stack trace points to `Pricing.Total` at line 16.

### Confidence
High. The failing tier, missing dictionary entry, introduced lookup, and resulting exception are directly confirmed by the tests and source.