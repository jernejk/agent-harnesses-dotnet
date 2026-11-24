### Symptom
Pricing for `standard`-tier customers throws `KeyNotFoundException`. The order-total API consequently returns HTTP 500.

### Reproduction
- `dotnet test`: 3 failed, 5 passed. Both standard-tier pricing tests fail with a missing `standard` dictionary key; the API test for order 1001 gets `InternalServerError`.
- `GET /orders/1001/total` returns HTTP 500.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary defined at lines 7–11 contains only `gold` and `silver`. The previously supported `standard` tier is absent, so pricing throws when it encounters that tier.

### Evidence
At v1.3.0, the discount was `customer.Tier == "gold" ? 0.05m : 0m`, which handled `standard`. The new tests still exercise `standard`, while the new lookup omits it. Git blame attributes the failing lookup line to `9bfbf7d`.

### Confidence
High. The failing stack trace, API response, source diff, and test expectations all point to the same cause.