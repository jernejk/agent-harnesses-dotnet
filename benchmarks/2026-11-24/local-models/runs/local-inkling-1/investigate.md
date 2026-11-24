Investigation complete — no fix applied (investigation phase only). Findings below.

**Status page:** I could not open https://status.example.com — no internet access from this environment.

**Symptom**
- `GET /orders/{id}/total` returns HTTP 500 since the 1.4.0 deploy (1.3.0 was fine).
- Tests fail: `PricingTests.Totals_round_to_cents`, `PricingTests.Standard_customers_pay_list_price_plus_gst`, and `OrdersApiTests.Order_totals_are_available(1001)` — all throwing `System.Collections.Generic.KeyNotFoundException: The given key 'standard' was not present in the dictionary` at `Pricing.cs:line 16`.

**Reproduction**
- `dotnet test` → 3 failed, 5 passed.
- `http_get /orders/1001/total` → `500` (order 1001 belongs to customer 2, tier "standard").

**Change that caused it**
- Commit `9bfbf7d` — "Move tier discounts into a lookup table and add silver tier" (in 1.4.0, released as `f1812c8` "Release 1.4.0"). It rewrote the discount logic from `customer.Tier == "gold" ? 0.05m : 0m` to a `TierDiscounts` dictionary containing only `gold` and `silver`.
- The same commit also flipped order 3 / customer 3 to `silver`, so gold/silver now work, but the `standard` tier has no entry.

**Root cause**
- `src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];` indexes the dictionary directly. For any `standard`-tier customer (e.g. customer 2 / order 1001, and the standard-customer test fixtures), `customer.Tier` is `"standard"`, which is absent from `TierDiscounts`, so `Dictionary.get_Item` throws `KeyNotFoundException`, which the API surfaces as HTTP 500.
- The 404-problem-details change (`ab38e53`) and the version bump (`f1812c8`) are unrelated to the 500.

**Evidence**
- `dotnet test` stack trace: `at Orders.Api.Pricing.Total(Order order, Customer customer) in /_/src/Orders.Api/Pricing.cs:line 16`.
- `git show 9bfbf7d` diff: dictionary literal with only `["gold"] = 0.05m` and `["silver"] = 0.02m`; call site changed to `TierDiscounts[customer.Tier]`.
- `OrderStore.cs`: customers 1=gold, 2=standard, 3=silver; order 1001 → customer 2 (standard), order 1002 → customer 1 (gold), order 1003 → customer 3 (silver). So the 500 hits every standard-tier order.
- `GET /orders/1001/total` → 500; the new silver test passes (gold/silver paths work), standard-tier tests fail.

**Confidence**
- High. The failing test output, the dictionary contents, and the store data all agree. The only untested hypothesis would be an unrelated failure, but the stack trace pins it to the exact line.

**Suggested fix (for the engineer, not applied):** add `["standard"] = 0.00m` to the `TierDiscounts` dictionary in `Pricing.cs` (or use `GetValueOrDefault(customer.Tier, 0m)`), and re-run the suite. The new silver test should keep passing; the two standard-tier tests and the API test should then go green.