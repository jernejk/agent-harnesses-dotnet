### Symptom
Standard-tier order totals throw `KeyNotFoundException` for key `standard`. The API returns **500 Internal Server Error** for order 1001.

### Reproduction
- `dotnet test`: **3 failed, 5 passed**. Two pricing tests fail with the missing-key exception; the order-total API test fails because it receives a 500 instead of 200.
- `GET /orders/1001/total`: **500**.

### Change that caused it (commit and subject)
`9bfbf7d` — **“Move tier discounts into a lookup table and add silver tier”**. It replaced the prior default-zero discount logic with a dictionary lookup. The following commits up to v1.4.0 do not correct the missing standard-tier entry.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`. A `standard` customer therefore causes the lookup to throw instead of receiving the previous 0% discount. Order 1001 is assigned to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs`).

### Evidence
The failing test stack traces point to `Pricing.Total` at line 16. The existing pricing tests still exercise the `standard` tier, and the order store assigns that tier to the customer for order 1001.

### Confidence
**High** — the reproduced exception, affected data, and regression introduced by the commit all align. No files were changed.