### Symptom
GET `/orders/1001/total` returns HTTP 500.

### Reproduction
- `dotnet test`: 3 failed, 5 passed. Two pricing tests and the API test fail; the exception is `KeyNotFoundException` for key `'standard'`.
- GET `/orders/1001/total` returned 500.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. It replaced the previous standard-tier fallback with a direct dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 contains only `"gold"` and `"silver"`. Order 1001 belongs to customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws. The total route calls pricing at `src/Orders.Api/Program.cs:24`, producing the 500.

### Evidence
The release diff shows the change from a conditional that gave non-gold customers no discount to `TierDiscounts[customer.Tier]`; the table added by that commit omits `"standard"`. The API test and standard-tier pricing tests fail with the same missing-key exception. I could not access `https://status.example.com` because internet access is unavailable.

### Confidence
High. No files were changed; this was an investigation only.