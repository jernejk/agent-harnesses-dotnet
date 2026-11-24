### Symptom
`GET /orders/1001/total` returns HTTP 500, although `/health` returns 200 and reports version 1.4.0.

### Reproduction
`dotnet test` failed 3 of 8 tests. The two pricing tests and the API totals test failed with a `KeyNotFoundException` for key `standard`. A direct request to `/orders/1001/total` also returned 500.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 contains only `gold` and `silver`. The seeded order 1001 belongs to a `standard` customer, so this lookup throws instead of applying the prior zero-discount behavior. The exception propagates from the total endpoint’s pricing call in `src/Orders.Api/Program.cs:24`.

### Evidence
- `src/Orders.Api/OrderStore.cs:8,14`: customer 2 is `standard`; order 1001 belongs to customer 2.
- The v1.3.0 implementation applied a 5% discount to `gold` and zero discount to other tiers.
- The test run and direct API request reproduced the failure.
- I can’t access `https://status.example.com` because internet access is unavailable.

### Confidence
High. The failing tier, missing lookup entry, exception, and post-v1.3.0 change all align. No files were changed.