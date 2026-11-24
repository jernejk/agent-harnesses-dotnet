## Symptom
`GET /orders/1001/total` returns HTTP 500 on v1.4.0.

## Reproduction
`GET /orders/1001/total` returned 500. `dotnet test` failed 3 of 8 tests, including the API total test and two pricing tests; the exception was `KeyNotFoundException` for key `standard`.

## Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **Move tier discounts into a lookup table and add silver tier**.

## Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer’s tier. The lookup at lines 7–11 contains `gold` and `silver`, but not `standard`. Order 1001 belongs to a standard-tier customer, so the index throws and the API returns 500. The previous code treated non-gold tiers as having no discount.

## Evidence
The test stack trace points to `Pricing.Total` at `Pricing.cs:16`. `src/Orders.Api/OrderStore.cs:14` shows order 1001 belongs to customer 2, whose tier is `standard` at line 8. The change in commit `9bfbf7d` replaced the fallback pricing logic with the incomplete dictionary lookup.

Could not check `https://status.example.com`: no internet access is available. No code was changed; this was investigation-only.

## Confidence
High.