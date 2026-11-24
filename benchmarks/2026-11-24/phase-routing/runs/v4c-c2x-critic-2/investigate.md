### Symptom
`GET /orders/1001/total` returns HTTP 500. This is the standard-tier order; the gold and silver orders, 1002 and 1003, return 200.

### Reproduction
`dotnet test` failed: 3 failed, 5 passed. The failures include `KeyNotFoundException` for key `'standard'` and an API test expecting 200 but receiving `InternalServerError`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. The change replaced the prior gold-only conditional with a dictionary lookup but added entries only for `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws for `"standard"`, which is not in the table defined at lines 7–11. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`), so the exception produces the 500.

### Evidence
The API reproduces the 500 for order 1001, while orders 1002 and 1003 return 200. The standard-tier pricing tests also fail with the same missing-key exception. **Status page:** I can’t access external URLs in this environment (no internet access), so I could not verify an incident there.

### Confidence
High. The failure, exception, changed lookup, and affected customer tier all agree. No code was modified; this was an investigation only.