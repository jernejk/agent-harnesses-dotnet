I have everything needed. Note the system instructions for this phase: investigate and report — not fix. Here are my findings.

**Status page:** I could not open https://status.example.com — this environment has no internet access and no curl, so I could not check it.

## Findings

**Symptom**
`GET /orders/{id}/total` returns HTTP 500 since the 1.4.0 deploy. Specifically `/orders/1001/total` → 500, and `dotnet test` fails 3 tests (`PricingTests.Totals_round_to_cents`, `PricingTests.Standard_customers_pay_list_price_plus_gst`, `OrdersApiTests.Order_totals_are_available(orderId: 1001)`), all with `System.Collections.Generic.KeyNotFoundException: The given key 'standard' was not present in the dictionary`.

**Reproduction**
- `dotnet test` → 3 failed, 5 passed (shown above).
- `http_get /orders/1001/total` → `500`.
- 1.3.0 was fine (tag exists; failures introduced in 1.4.0).

**Change that caused it**
Commit `9bfbf7d` — "Move tier discounts into a lookup table and add silver tier" (part of the 1.4.0 release, tagged `f1812c8` "Release 1.4.0"). The diff changed `Pricing.cs` to a `Dictionary<string, decimal>` with only `["gold"]` and `["silver"]` entries, and changed `OrderStore.cs` so customer 3 (Mia Chen) is now `"silver"` instead of `"standard"`.

**Root cause (file:line)**
`src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];`. The new lookup table omits `"standard"`, so any order whose customer is `standard` (e.g. order 1001 → customer 2, Noah Patel) throws `KeyNotFoundException`, which the API surfaces as HTTP 500. The silver-tier change added a `"silver"` entry and re-tiered customer 3, but no `"standard"` (0%) entry was added — previously `customer.Tier == "gold" ? 0.05m : 0m` handled the standard case implicitly.

**Evidence**
- Test failure stack trace: `at Orders.Api.Pricing.Total(Order order, Customer customer) in /_/src/Orders.Api/Pricing.cs:line 16` (`Dictionary.get_Item`).
- `http_get /orders/1001/total` → 500.
- `git diff v1.3.0..HEAD` shows the table and the missing `"standard"` key.
- `OrderStore.cs`: customer 2 (`"standard"`) is still mapped, and order 1001 belongs to customer 2 — so the 500 is reproducible for that order.
- `OrdersApiTests.Order_totals_are_available(1001)` fails with `Expected: OK / Actual: InternalServerError`, while `1002` (gold) and `1003` (silver) pass — confirming the failure is specific to `standard` customers.

**Confidence**
High. The exception, the diff, and the API response all point to the same single-line cause. (I did not apply any fix, per the investigation-phase instructions.)

A likely fix for the engineer to plan: add `["standard"] = 0.00m` to `TierDiscounts` (and ideally a fallback/default so an unknown tier fails gracefully rather than 500s), then re-run `dotnet test`. I have not made that change.