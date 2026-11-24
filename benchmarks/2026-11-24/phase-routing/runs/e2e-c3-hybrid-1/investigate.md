### Symptom
`GET /orders/1001/total` returns HTTP 500. The failure affects the `standard`-tier order; gold and silver orders succeed.

### Reproduction
- `GET /orders/1001/total` → 500.
- `GET /orders/1002/total` (gold) and `/orders/1003/total` (silver) → 200.
- `dotnet test` → 3 failed, 5 passed. The failures include `KeyNotFoundException` for key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` directly indexes a dictionary that only defines `gold` and `silver` (lines 7–11). Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException`, which becomes the HTTP 500. Before this change, non-gold customers received a zero discount.

### Evidence
The v1.3.0-to-v1.4.0 diff shows the change from a gold-only conditional with a zero-discount fallback to the direct dictionary lookup. `git blame` attributes the lookup line and dictionary to the regression commit. The standard-tier tests still expect successful totals (`tests/Orders.Tests/PricingTests.cs:13–14,25–26`).

Status page: I can’t access `https://status.example.com` from this environment.

### Confidence
High. No files were changed.