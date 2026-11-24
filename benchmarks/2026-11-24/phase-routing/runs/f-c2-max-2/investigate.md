## Symptom
GET `/orders/1001/total` returns HTTP 500. The failure affects standard-tier orders; gold and silver order totals succeed.

## Reproduction
- `GET /orders/1001/total` → **500**
- `GET /orders/1002/total` (gold) and `/orders/1003/total` (silver) → **200**
- `dotnet test` → **3 failed, 5 passed**; failures include `KeyNotFoundException` for key `standard`.
- `/health` returns **200** and reports version `1.4.0`.

## Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.

## Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 has entries for `gold` and `silver`, but none for `standard`, so standard-tier totals throw `KeyNotFoundException`. Order 1001 is linked to a standard customer (`src/Orders.Api/OrderStore.cs:8,14`); the endpoint calls pricing at `src/Orders.Api/Program.cs:24`.

## Evidence
Git diff and blame attribute the lookup change to `9bfbf7d`; the test output confirms the missing `standard` key. The working tree is clean, and no fixes were made. I could not access `https://status.example.com` because internet access is unavailable here.

## Confidence
High.