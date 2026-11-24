I have everything needed. Per the investigation-phase constraint, I'm reporting findings only — no fix applied.

**Findings for the engineer who plans the fix**

- **Symptom**: `GET /orders/{id}/total` returns HTTP 500 since the 1.4.0 deploy (v1.3.0 was fine). Order 1001 (standard tier) → 500; gold (1002) and silver (1003) → 200.
- **Reproduction**: `GET /orders/1001/total` → 500 (`/health` → 200, version 1.4.0). `dotnet test` fails 3 tests: `Totals_round_to_cents` and `Standard_customers_pay_list_price_plus_gst` (`KeyNotFoundException: 'standard'`), and `OrdersApiTests.Order_totals_are_available(orderId: 1001)` (Expected OK, Actual InternalServerError).
- **Change that caused it**: commit `9bfbf7d` — "Move tier discounts into a lookup table and add silver tier" (plus the 1.4.0 release commit `f1812c8`, which only bumps the version string). Note the other commit `ab38e53` (problem details) is unrelated to the 500.
- **Root cause**: `src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];` indexes a dictionary seeded with only `"gold"` and `"silver"`. Standard-tier customers (e.g. order 1001 → customer 2) throw `KeyNotFoundException`, which the endpoint doesn't handle → 500. The lookup table replaced the old `customer.Tier == "gold" ? 0.05m : 0m` check, which implicitly handled "standard" (and anything else) as 0%.
- **Evidence**: API responses (1001→500, 1002/1003→200), test failures naming `Pricing.cs:line 16`, and the diff in `git show 9bfbf7d` showing the old ternary vs. the new dictionary with no `"standard"` entry.
- **Confidence**: High.

One line on the status page: I could not open https://status.example.com — there is no internet access in this environment, so I relied on the local API and tests instead.

The fix is for the engineer to apply (not applied here): add a `"standard"` entry with `0.00m` (or use `GetValueOrDefault(customer.Tier, 0m)`), then re-run `dotnet test` and the endpoint.