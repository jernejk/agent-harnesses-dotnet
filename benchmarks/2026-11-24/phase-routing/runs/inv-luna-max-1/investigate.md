### Symptom
Standard-customer order totals fail with an internal server error. The other tested tiers, gold and silver, return totals successfully.

### Reproduction
- `dotnet test`: **3 failed, 5 passed**. Failures include two pricing tests and the API test for order `1001`; the exception is `KeyNotFoundException` for key `"standard"`.
- `GET /orders/1001/total`: **500**.
- `GET /orders/1002/total` and `/orders/1003/total`: **200**.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the lookup at lines 7–11 contains only `"gold"` and `"silver"`. Order `1001` belongs to customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws. Before this change, standard tiers fell through to a zero discount.

### Evidence
`git blame` attributes the direct lookup at `Pricing.cs:16` to `9bfbf7d`; the `v1.3.0` version used the gold-only conditional with a zero-discount fallback. The test stack trace points to line 16, and the API reproduces the resulting 500.

### Confidence
**High** — the failing key, customer/order mapping, changed line, and API behavior all agree. No files were changed.