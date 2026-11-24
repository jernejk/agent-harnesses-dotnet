### Symptom
Order totals fail for standard-tier customers. The test suite reports **3 failed, 5 passed**; `/orders/1001/total` returns **500**, while `/health` returns 200.

### Reproduction
`dotnet test` fails in two pricing tests and the API test for order 1001. The pricing failures throw `KeyNotFoundException` for key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The lookup defines `gold` and `silver`, but not `standard`. Order 1001 belongs to a standard-tier customer, so the dictionary indexer throws instead of calculating the total.

### Evidence
The commit replaced the prior gold-or-zero discount logic with the dictionary lookup. `OrderStore.cs` still assigns Noah Patel the `standard` tier and maps order 1001 to that customer. The test stack trace points to `Pricing.cs:line 16`; the API request reproduces the resulting 500.

### Confidence
High.