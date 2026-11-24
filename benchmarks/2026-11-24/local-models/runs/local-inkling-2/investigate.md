Note: I could not check https://status.example.com — no internet/curl access is available in this environment, so I relied on the repo and API instead.

**Symptom** — `GET /orders/{id}/total` returns HTTP 500 since the 1.4.0 deploy (1.3.0 was fine).

**Reproduction** — `GET /orders/1001/total` → 500. `dotnet test` fails 3 tests:
- `PricingTests.Totals_round_to_cents` and `Standard_customers_pay_list_price_plus_gst`: `KeyNotFoundException: The given key 'standard' was not present in the dictionary` at `Pricing.cs:line 16`.
- `OrdersApiTests.Order_totals_are_available(orderId: 1001)`: expected OK, got InternalServerError.
- (Orders 1002/1003, whose customers are gold/silver, return 200 — standard-tier orders are the ones that 500.)

**Change that caused it** — commit `9bfbf7d` ("Move tier discounts into a lookup table and add silver tier", Priya Nair, 2026-11-13), part of the 1.4.0 release (`f1812c8`). It changed `Pricing.cs` from `customer.Tier == "gold" ? 0.05m : 0m` to a `TierDiscounts` dictionary containing only `["gold"] = 0.05m` and `["silver"] = 0.02m` — no `"standard"` entry — and added `["silver"]` handling while the store still assigns standard-tier customers (order 1001 → customer 2 "Noah Patel", tier "standard").

**Root cause** — `src/Orders.Api/Pricing.cs:16`: `var discount = TierDiscounts[customer.Tier];` throws `KeyNotFoundException` for any customer whose tier is `"standard"` (or any tier not in the table), producing the 500 on `/orders/{id}/total`. The prior code defaulted non-gold tiers to 0% discount; the lookup table silently dropped that default.

**Evidence** — API 500 on order 1001; the two failing pricing tests name the missing `'standard'` key; the v1.3.0→HEAD diff shows the lookup table and the `"standard"` customer unchanged in `OrderStore.cs`; gold (1002) and silver (1003) orders return 200 with correct totals.

**Confidence** — High. One caveat: the `OrderStore.cs` change (customer 3 → silver) is unrelated to the 500; it's the `Pricing.cs` table that breaks.

I have not applied any fix — this is the investigation phase. The natural fix (for the engineer to plan) is to add a `"standard"` entry (0.00m) or make the lookup default to 0% for unknown/standard tiers, e.g. `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`.