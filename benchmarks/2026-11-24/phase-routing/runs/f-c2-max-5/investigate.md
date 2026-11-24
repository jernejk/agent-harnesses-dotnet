### Symptom
`GET /orders/1001/total` returns HTTP 500. `GET /orders/1001` succeeds.

### Reproduction
- `dotnet test`: **3 of 8 tests failed**. The standard-tier pricing tests throw `KeyNotFoundException` for key `standard`; the API test gets `InternalServerError` instead of `OK`.
- Local `GET /orders/1001/total` returned **500**. The order has `customerId: 2`.
- Status page: I couldn’t access `https://status.example.com` from this environment.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. It replaced the previous gold-or-zero fallback with a dictionary lookup containing only `gold` and `silver`.

### Root cause (file:line and why)
In `src/Orders.Api/Pricing.cs:7-11,16`, the discount dictionary has no `standard` entry, but `TierDiscounts[customer.Tier]` indexes it directly. Order 1001’s customer is `standard` (`src/Orders.Api/OrderStore.cs:8`), so pricing throws `KeyNotFoundException`. The handler calls pricing at `src/Orders.Api/Program.cs:23-24`, resulting in the HTTP 500.

### Evidence
At `v1.3.0`, pricing used `customer.Tier == "gold" ? 0.05m : 0m`, so standard customers received zero discount instead of causing an exception. The test stack trace also points directly to `Pricing.cs:16`. No fix was applied; this was an investigation-only phase.

### Confidence
High. The failing request, exception, stored customer tier, and regression in the diff all agree.