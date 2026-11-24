### Symptom
`GET /orders/1001/total` returns HTTP 500. Order 1001 belongs to a `standard` customer.

### Reproduction
- `GET /orders/1001/total` → **500**
- `dotnet test` → **3 failed, 5 passed**; failures include two pricing tests and the API total test.
- As a comparison, `/orders/1002/total` and `/orders/1003/total` return **200**.
- I couldn’t access `https://status.example.com` from this environment.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit is included in the v1.4.0 release.

### Root cause (file:line and why)
[`src/Orders.Api/Pricing.cs:16`](src/Orders.Api/Pricing.cs#L16) indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 contains only `gold` and `silver`. For the `standard` tier assigned to customer 2 in [`src/Orders.Api/OrderStore.cs:8`](src/Orders.Api/OrderStore.cs#L8), the lookup throws `KeyNotFoundException`. The endpoint calls this pricing method at [`src/Orders.Api/Program.cs:24`](src/Orders.Api/Program.cs#L24), producing the 500.

### Evidence
The v1.3.0-to-v1.4.0 diff shows this commit replaced the prior “gold discount, otherwise no discount” logic with the dictionary lookup. `git blame` attributes the lookup and table to `9bfbf7d`; the test failure stack also points to `Pricing.cs:16`.

### Confidence
**High.** The failing request, test output, source, and release diff all agree. No code was changed; this was an investigation only.