### Symptom
Order total calculations for standard-tier customers throw `KeyNotFoundException`; the affected API request returns HTTP 500.

### Reproduction
`dotnet test` reproduced 3 failures out of 8: two pricing tests and the order-total API test for order `1001`. A direct `GET /orders/1001/total` also returned HTTP 500. The health endpoint returned 200.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This is the relevant regression between `v1.3.0` and `HEAD`; the other intervening commit concerns unknown-order responses.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the table defined at lines 7–11 contains only `gold` and `silver`. Standard-tier customers therefore trigger `KeyNotFoundException`. Before this change, non-gold customers received a zero discount.

### Evidence
Order `1001` belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs`). The exception points to `Pricing.Total` line 16, and the test failure reports the missing key as `'standard'`.

### Confidence
High. The failing value, customer data, exception location, and regression diff all align.