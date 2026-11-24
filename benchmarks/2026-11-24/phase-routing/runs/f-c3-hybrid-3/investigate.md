Status page: I couldn’t access `https://status.example.com` with the available tools.

### Symptom
`GET /orders/1001/total` returns HTTP 500.

### Reproduction
The local API returned 500 for `/orders/1001/total`. `dotnet test` reproduced three failures: the order 1001 total API test and two pricing tests, all reporting a missing `standard` key.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`. Order 1001 belongs to a `standard` customer (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException` and results in HTTP 500. In v1.3.0, non-gold tiers received a zero discount instead.

### Evidence
The `dotnet test` stack trace points to `Pricing.Total` at `Pricing.cs:16`; the release diff shows that lookup replacing the v1.3.0 conditional. No files were changed.

### Confidence
High.