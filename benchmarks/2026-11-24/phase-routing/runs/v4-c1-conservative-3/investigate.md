**Status page:** I can’t access `https://status.example.com` from this environment.

### Symptom
`GET /orders/1001/total` returns HTTP 500 locally.

### Reproduction
`dotnet test` fails 3 of 8 tests. The pricing tests throw `KeyNotFoundException` for key `standard`, and the API test receives `InternalServerError` instead of `OK`.

### Change that caused it (commit and subject)
`9bfbf7d` — **“Move tier discounts into a lookup table and add silver tier”**. It is included in `v1.4.0`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws when the tier is `standard`, because the table at lines 7–11 contains only `gold` and `silver`. Order `1001` belongs to customer `2`, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8–9, 14`). The exception propagates from the total route (`src/Orders.Api/Program.cs:23–24`), producing the 500.

### Evidence
In `v1.3.0`, `Pricing.Total` used `customer.Tier == "gold" ? 0.05m : 0m`, so standard customers got no discount. The new dictionary lookup removed that fallback. The existing standard-tier pricing tests expect list price plus GST.

### Confidence
High. The failing request and tests reproduce the error, and the diff identifies the missing `standard` entry as the cause. No files were changed.