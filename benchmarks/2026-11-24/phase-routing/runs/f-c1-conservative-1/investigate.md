### Symptom
`GET /orders/1001/total` returns HTTP 500. Orders 1002 and 1003 return 200.

### Reproduction
`dotnet test` failed 3 of 8 tests. The failures include both standard-tier pricing tests and the endpoint test for order 1001.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This replaced the prior rule that gave non-gold customers no discount with a lookup table containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for `"standard"`, which is not in the table (lines 7–11). Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8`), and the total route calls this pricing code (`src/Orders.Api/Program.cs:24`).

### Evidence
The test failures report a missing `"standard"` key. The v1.3.0 implementation treated every non-gold tier as zero discount, so standard customers worked before the change. I couldn’t access the status page from this environment. No fix was made.

### Confidence
High: the failure reproduces, the exception identifies the missing key, and the post-v1.3.0 diff introduced the incomplete lookup.