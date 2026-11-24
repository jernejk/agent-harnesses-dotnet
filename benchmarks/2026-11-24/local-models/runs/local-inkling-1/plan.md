Verification is done and everything in the findings checks out — no discrepancies found:

- `dotnet test` → 3 failed / 5 passed, same failures, same stack trace (`KeyNotFoundException: 'standard'` at `Pricing.cs:line 16`).
- `git show 9bfbf7d` confirms the dictionary only has `gold`/`silver`, and the call site changed from `customer.Tier == "gold" ? 0.05m : 0m` to `TierDiscounts[customer.Tier]`.
- 1.3.0 behavior (`fcded06`): any non-gold tier → 0 discount.
- Store: customer 2 (standard) owns order 1001; customers 1/3 are gold/silver. No other tier lookup exists (`grep` confirms only `Pricing.cs` indexes the dictionary; the endpoint just calls `Pricing.Total`).

Here is the plan for the engineer.

---

**Root cause** (file:line)
- `src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];`
- The `TierDiscounts` dictionary (lines 7–11) was added in commit `9bfbf7d` with only `["gold"] = 0.05m` and `["silver"] = 0.02m`. Commit `9bfbf7d` also flipped order 3 / customer 3 to `silver`, so gold and silver work, but `standard` — the tier of customer 2 (order 1001) and of the standard-customer test fixtures — has no entry. `Dictionary.get_Item` throws `KeyNotFoundException`, the API surfaces it as HTTP 500 (`Program.cs` has no exception handler; `OrdersApiTests` confirms 1001 → `InternalServerError`).

**Change** (one find-and-replace, one file)
- File: `src/Orders.Api/Pricing.cs`, line 16.
- Before:
  `        var discount = TierDiscounts[customer.Tier];`
- After:
  `        var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);`

**Why this is the smallest safe fix**
- It restores exactly the 1.3.0 behaviour for every input the regression broke: in 1.3.0 the discount was `0.05m` for gold and `0m` for everything else, and the dictionary now carries the two intended 1.4.0 tiers (gold 0.05, silver 0.02). With `GetValueOrDefault(customer.Tier, 0m)`, `standard` → 0m (as 1.3.0), and any unknown/future tier → 0m instead of throwing, which is also what 1.3.0 did — so no standard-tier or unlisted-tier order can 500 again.
- It keeps the intended 1.4.0 changes intact: the silver tier, its 0.02 rate, and the gold 0.05 rate are all preserved, and the dictionary-lookup approach is kept.
- It is a single-line find-and-replace in a single file, as required; no new tests, no refactor, no data changes. (Adding `["standard"] = 0.00m` to the dictionary would fix the reported cases but would still throw for any unlisted tier, and is not a one-line change — so it is not chosen.)

**Verification** (existing tests, no new tests)
- Run `dotnet test` after the fix. Expected: **8 passed, 0 failed**.
  - Previously failing, must now pass: `PricingTests.Totals_round_to_cents`, `PricingTests.Standard_customers_pay_list_price_plus_gst`, `OrdersApiTests.Order_totals_are_available(1001)`.
  - Previously passing, must stay passing: `PricingTests.Gold_customers_get_five_percent_off`, `PricingTests.Silver_customers_get_two_percent_off`, `OrdersApiTests.Order_totals_are_available(1002)`, `OrdersApiTests.Order_totals_are_available(1003)`, `OrdersApiTests.Unknown_orders_return_404`.
- Note: the deployed 1.4.0 service on localhost will not change until a redeploy (out of scope), so verification is via `dotnet test` only; the live `GET /orders/1001/total` will still 500 until then.

**Risks**
- An unknown/typo'd tier now silently prices at 0% instead of throwing. That is the intended 1.3.0 behaviour (non-gold → 0), so it is accepted; surfacing unknown tiers as a 4xx would be a larger, out-of-scope change.
- `GetValueOrDefault` on a `Dictionary<string, decimal>` returns `0m` for missing keys (decimal default), so no null-handling is needed.
- The fix does not touch `OrderStore.cs` (the gold/standard/silver data is correct), the silver test, or the API/404 handling — so the intended 1.4.0 behaviour and the 404-problem-details change (`ab38e53`) are unaffected.
- The 500 in production is not fixed until a redeploy; this plan only covers the repo change and test-based verification.