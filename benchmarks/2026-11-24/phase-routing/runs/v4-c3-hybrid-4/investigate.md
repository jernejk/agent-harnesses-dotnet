### Symptom
`GET /orders/1001/total` returns HTTP 500. I couldn’t open `https://status.example.com` in this environment.

### Reproduction
The local API returned 500 for `/orders/1001/total`. `dotnet test` failed 3 tests and passed 5; failures included the API test and pricing tests for standard-tier customers.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c` — **“Move tier discounts into a lookup table and add silver tier”**

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 contains only `gold` and `silver`, not `standard`. Order 1001 belongs to a standard-tier customer, so the lookup throws `KeyNotFoundException`, which results in the 500.

### Evidence
- `src/Orders.Api/OrderStore.cs:8,14`: customer 2 is `standard`; order 1001 belongs to customer 2.
- `src/Orders.Api/Program.cs:24`: the total route calls `Pricing.Total`.
- At v1.3.0, pricing applied a discount only for `gold` and otherwise used zero discount; the new lookup removed that fallback.

### Confidence
High. The reproduction, failing test stack trace, seeded order data, and release diff all point to the same cause. No changes were made.