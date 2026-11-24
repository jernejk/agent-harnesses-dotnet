### Symptom
`GET /orders/{id}/total` returns HTTP 500 for an order belonging to a standard-tier customer.

### Reproduction
`GET /orders/1001/total` returned 500. `dotnet test` had 3 failures out of 8; the pricing tests and the API total test failed with `KeyNotFoundException` for `"standard"`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. It replaced the previous non-gold fallback with a dictionary lookup containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws when the tier is `"standard"`, because the dictionary at lines 7–11 has no `"standard"` entry. Order 1001 belongs to customer 2, whose tier is `"standard"` in `src/Orders.Api/OrderStore.cs:8`. The exception is unhandled by the total endpoint and becomes a 500.

### Evidence
The test stack traces point to `Pricing.Total` at line 16. Git blame attributes that lookup to `9bfbf7d`; in v1.3.0, any non-gold tier received a zero discount. I couldn’t check the status page because internet access isn’t available.

### Confidence
High.