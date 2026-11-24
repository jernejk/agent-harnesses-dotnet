### Symptom
`GET /orders/1001/total` returns HTTP 500. The status page could not be checked because there is no internet access.

### Reproduction
- `GET /orders/1001/total` returned **500**.
- `dotnet test` failed **3 of 8** tests, including the standard-tier pricing tests and the API total test.
- Gold and silver totals succeeded: `/orders/1002/total` and `/orders/1003/total` returned **200**.

### Change that caused it (commit and subject)
`9bfbf7d` — **“Move tier discounts into a lookup table and add silver tier”**. It replaced the previous non-gold default discount with a dictionary lookup.

### Root cause (file:line and why)
In `src/Orders.Api/Pricing.cs:16`, `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for `"standard"` because the dictionary at lines 7–11 contains only `"gold"` and `"silver"`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`), and the endpoint calls pricing at `src/Orders.Api/Program.cs:24`, resulting in the 500.

### Evidence
At v1.3.0, `Pricing.cs` assigned a 5% discount to gold customers and **zero** to all others, so standard customers were supported. The test trace reports the missing `"standard"` key at `Pricing.cs:16`. No code was changed; this was an investigation-only phase.

### Confidence
High. The failing request, test exception, seeded order/customer data, and change from v1.3.0 all point to the same cause.