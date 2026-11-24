### Symptom
Standard-tier order totals fail with `KeyNotFoundException`; the API returns 500 for order 1001.

### Reproduction
- `dotnet test`: 3 failed, 5 passed. Two pricing tests fail because the key `standard` is absent; the order-total integration test for order 1001 receives `InternalServerError`.
- `GET /orders/1001/total` returns 500.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. The changes since `v1.3.0` also include `ab38e53` (“Return problem details for unknown orders”) and the `v1.4.0` release commit; the former only changes `Program.cs` and is unrelated.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` indexes a dictionary containing only `gold` and `silver` (lines 7–11). Standard customers therefore cause a `KeyNotFoundException`. Before this change, the pricing logic gave every non-gold tier a zero discount.

### Evidence
`src/Orders.Api/OrderStore.cs:8,14` shows customer 2 is standard and order 1001 belongs to that customer. The existing standard-tier tests expect list price plus GST (`tests/Orders.Tests/PricingTests.cs:14,26`). The `v1.3.0` implementation had a zero-discount fallback for non-gold customers.

### Confidence
High. The failing tests, API response, fixture data, and post-tag code change all point to the missing `standard` lookup entry. No files were changed during this investigation.