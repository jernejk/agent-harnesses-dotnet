### Symptom
`GET /orders/1001/total` returns HTTP 500. Order 1001 belongs to a `standard` customer.

### Reproduction
- `GET /orders/1001/total` → **500**
- `dotnet test` → **3 failed, 5 passed**; failures include the endpoint test and pricing tests, all with `KeyNotFoundException` for key `standard`.
- Control: `GET /orders/1002/total` → **200** for a gold customer.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. This commit is between v1.3.0 and v1.4.0.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The dictionary at lines 7–11 contains only `gold` and `silver`, but `src/Orders.Api/OrderStore.cs:8` still assigns a customer the `standard` tier. That missing key throws `KeyNotFoundException`, which surfaces as HTTP 500.

### Evidence
The commit changed the previous fallback behavior for non-gold customers to a dictionary lookup, without adding a `standard` entry. Git blame attributes the lookup and dictionary to the same commit. I could not access the external status page from this environment. **No files were changed.**

### Confidence
**High** — the endpoint response, test failures, stored customer tier, and change history all point to the same missing lookup key.