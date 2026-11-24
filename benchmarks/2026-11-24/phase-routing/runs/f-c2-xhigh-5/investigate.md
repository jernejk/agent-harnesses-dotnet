### Symptom
`GET /orders/1001/total` returns HTTP 500. The local status endpoint isn’t the external status page, and I can’t open `https://status.example.com` from this environment.

### Reproduction
- `GET /orders/1001/total` → **500**
- `GET /orders/1002/total` and `/orders/1003/total` → **200**
- `dotnet test` → **3 failed, 5 passed**. The failing tests throw `KeyNotFoundException` for key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This replaced the previous “gold gets a discount, all other tiers get zero” logic with a dictionary lookup containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The dictionary at lines 7–11 has no `standard` entry, so pricing order 1001 throws `KeyNotFoundException`, resulting in HTTP 500.

### Evidence
`src/Orders.Api/OrderStore.cs:14` assigns order 1001 to customer 2; lines 7–9 show that customer’s tier is `standard`. `src/Orders.Api/Program.cs:23–24` loads that customer and calls `Pricing.Total`. At `v1.3.0`, `Pricing.cs` used a conditional that treated every non-gold tier as a zero-discount tier, so `standard` did not throw.

### Confidence
High. The observed API and test failures match the missing dictionary key, and git blame attributes the direct lookup to the identified commit. No files were modified.