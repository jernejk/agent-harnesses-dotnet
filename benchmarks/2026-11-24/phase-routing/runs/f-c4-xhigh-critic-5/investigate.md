### Symptom
`GET /orders/1001/total` returns HTTP 500. The order belongs to a `standard` customer.

### Reproduction
- Local API: `GET /orders/1001/total` returned 500; `GET /orders/1001` returned 200.
- `dotnet test`: 3 failed, 5 passed. The failures included `KeyNotFoundException` for key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit replaced the previous “gold discount, otherwise zero” logic with a dictionary lookup. It is in the history after v1.3.0.

### Root cause (file:line and why)
In `src/Orders.Api/Pricing.cs:7–11`, the discount table includes `gold` and `silver`, but not `standard`. `src/Orders.Api/Pricing.cs:16` directly indexes it with `customer.Tier`, so standard orders throw `KeyNotFoundException`. The endpoint calls this pricing method at `src/Orders.Api/Program.cs:24`, resulting in the 500.

### Evidence
`src/Orders.Api/OrderStore.cs:8` defines customer 2 as `standard`, and `src/Orders.Api/OrderStore.cs:14` assigns that customer to order 1001. The test failures point to `Pricing.cs:16`.

Status page: I couldn’t access `https://status.example.com` from this environment.

### Confidence
High. The API response, test failures, order/customer data, and commit diff all support the same cause. No code was changed; this was an investigation only.