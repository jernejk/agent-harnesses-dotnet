## Symptom
`GET /orders/1001/total` returns HTTP 500. I couldn’t access the external status page from this environment.

## Reproduction
- `dotnet test` failed 3 tests. The endpoint test for order 1001 returned `InternalServerError`; two pricing tests threw `KeyNotFoundException` for `standard`.
- `GET /orders/1001/total` returned 500, while orders 1002 and 1003 returned 200.

## Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit replaced the previous default-to-zero discount logic with a lookup table. It is included in the `v1.4.0` release.

## Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table at lines 7–11 contains only `gold` and `silver`, so a `standard` customer throws `KeyNotFoundException`, which causes the endpoint’s 500.

## Evidence
`src/Orders.Api/OrderStore.cs:14` maps order 1001 to customer 2; line 8 identifies customer 2 as `standard`. In `v1.3.0`, `Pricing.Total` used a conditional that assigned a zero discount to non-gold customers. The post-release change introduced the lookup without a `standard` entry.

## Confidence
High. The failing tier, exception, endpoint behavior, and regression-producing code change all align. No fix was made.