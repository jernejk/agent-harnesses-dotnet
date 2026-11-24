### Symptom
Order totals for standard-tier customers fail. `Pricing.Total` throws a `KeyNotFoundException`; the API returns HTTP 500.

### Reproduction
- `dotnet test`: 3 failed, 5 passed. Both standard-customer pricing tests fail with “key `standard` was not present”; the order-total API test also fails.
- `GET /orders/1001/total` returns 500.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. The change replaced the prior gold-or-zero discount logic with a dictionary lookup, but added entries only for `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The lookup is defined at lines 7–11 and has no `standard` entry, so standard customers trigger the exception. `src/Orders.Api/OrderStore.cs:8` assigns customer 2 the `standard` tier; order 1001 uses that customer.

### Evidence
- The diff from `v1.3.0` shows the lookup replacing the previous fallback behavior.
- `git blame` attributes the lookup and line 16 to `9bfbf7d`.
- The failing tests and API response reproduce the resulting error.

### Confidence
High.