## Symptom
`GET /orders/1001/total` returns HTTP 500. The regression affects **standard-tier** orders, not every order: gold and silver order totals still succeed.

## Reproduction
- `GET /orders/1001/total` → **500**
- `dotnet test` → **3 failed, 5 passed**. The failures include both standard-tier pricing tests and `Order_totals_are_available(orderId: 1001)`.
- `GET /orders/1002/total` and `/orders/1003/total` → **200**

## Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**

The change replaced the previous fallback—gold gets a discount, all other tiers get none—with a dictionary lookup.

## Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for `"standard"`. The table at lines 7–11 contains only `"gold"` and `"silver"`.

The total route calls this method at `src/Orders.Api/Program.cs:23–24`. Order 1001 uses customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup causes the 500.

## Evidence
- At v1.3.0, `Pricing.Total` gave every non-gold tier a zero discount; the new lookup removed that fallback.
- `PricingTests.cs:14,26` still exercise standard-tier pricing and fail under the new lookup.
- The endpoint succeeds for the seeded gold and silver orders, confirming the issue is specific to tiers absent from the table.
- I could not access the external status page from this environment (no internet access).
- No code was changed; this was an investigation only.

## Confidence
**High.** The API response, test failures, seeded customer data, and v1.3.0-to-v1.4.0 pricing diff all point to the same missing `standard` entry/fallback.