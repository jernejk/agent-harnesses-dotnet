### Symptom
`GET /orders/1001/total` returns HTTP 500.

### Reproduction
- Called the local API: `/orders/1001/total` returned 500.
- Ran `dotnet test`: **3 failed, 5 passed**. Failures include both standard-tier pricing tests and the API total test.

### Change that caused it (commit and subject)
`9bfbf7d` — **“Move tier discounts into a lookup table and add silver tier”**. It replaced the standard-tier fallback with a dictionary lookup, but the new table contains only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for a `standard` customer because `standard` is absent from the table at lines 7–11. Order 1001 uses customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`); the total endpoint calls this pricing method at `src/Orders.Api/Program.cs:24`.

### Evidence
The test exception names the missing key `'standard'` and points to `Pricing.cs:16`. The prior code in the git diff treated non-gold tiers as receiving no discount, so standard-tier pricing worked before this change. The later 1.4.0 change for unknown-order responses is unrelated.

### Confidence
High. The failure is reproduced, and the exception, changed pricing logic, and seeded order/customer data all match. No code was changed during this investigation.