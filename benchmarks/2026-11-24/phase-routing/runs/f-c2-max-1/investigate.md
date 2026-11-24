### Symptom
`GET /orders/1001/total` returns HTTP 500.

### Reproduction
The local API returned 500. `dotnet test` exited with 3 failures and 5 passes; the failures include the order 1001 API test and two pricing tests, all reporting a missing `standard` key.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. It replaced the v1.3.0 fallback for non-gold customers with a lookup table.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer’s tier, but the table at lines 7–11 contains only `gold` and `silver`. Order 1001 maps to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException`. The total endpoint calls this pricing method at `src/Orders.Api/Program.cs:23–24`, resulting in the 500.

### Evidence
The v1.3.0 implementation treated non-gold tiers as having no discount; the new implementation has no `standard` entry. The test failures and API response reproduce the resulting exception. No code was changed; this was an investigation-only task.

### Confidence
High.

I couldn’t access `https://status.example.com` because this environment has no internet access.