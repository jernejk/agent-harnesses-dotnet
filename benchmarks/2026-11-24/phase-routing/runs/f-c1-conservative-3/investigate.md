Status page: I couldn’t access https://status.example.com from this environment.

### Symptom
`GET /orders/1001/total` returns HTTP 500.

### Reproduction
The local API returned 500 for `/orders/1001/total`. `dotnet test` failed 3 of 8 tests; the failures include the endpoint test and pricing tests, with a `KeyNotFoundException` for the key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`. Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws and the endpoint returns 500.

### Evidence
The endpoint calls `Pricing.Total` in `src/Orders.Api/Program.cs:24`. The commit replaced the previous gold-versus-default-zero discount logic with direct dictionary indexing, without adding a `standard` entry. The working tree was clean; **no fix was made**.

### Confidence
High. The reproduced exception, endpoint/customer mapping, and commit diff all point to the same cause.