### Symptom
Pricing calls for standard-tier customers throw `KeyNotFoundException`; the API returns 500 for order 1001.

### Reproduction
`dotnet test` produced 3 failures and 5 passes. The failures were `Standard_customers_pay_list_price_plus_gst`, `Totals_round_to_cents`, and the API test for order 1001.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer’s tier. The new map at lines 7–11 contains `gold` and `silver`, but not `standard`, so standard-tier pricing throws instead of applying the prior zero-discount fallback.

### Evidence
`src/Orders.Api/OrderStore.cs:14` maps order 1001 to customer 2; line 8 identifies that customer as `standard`. The regression diff replaced the prior “gold gets 5%, otherwise 0%” logic with the direct lookup. The API test reports `InternalServerError` for order 1001.

### Confidence
High. The failing tests, stored order/customer data, and changed pricing logic all point to the same cause.