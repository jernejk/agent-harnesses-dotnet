I've verified the findings against the repo and tests. The report is accurate — I'll summarize what I checked and then give the plan.

**Verification performed (read-only):**
- `Pricing.cs` line 16 is exactly the reported lookup: `var discount = TierDiscounts[customer.Tier];`, and the table contains only `["gold"] = 0.05m` and `["silver"] = 0.02m` — no `"standard"` entry.
- `git show 9bfbf7d` confirms the regression: 1.3.0 used `customer.Tier == "gold" ? 0.05m : 0m` (default 0% for everything else); 1.4.0 replaced it with the lookup table and added silver.
- `dotnet test` reproduces the failure exactly as reported: 3 failed / 5 passed — `Totals_round_to_cents` and `Standard_customers_pay_list_price_plus_gst` (`KeyNotFoundException: 'standard'` at Pricing.cs:line 16) and `Order_totals_are_available(1001)` (expected OK, got InternalServerError).
- `OrderStore.cs` still assigns customer 2 (Noah Patel, order 1001) as tier `"standard"`; gold (1002) and silver (1003) orders are unaffected.

---

# Plan: Fix the 1.4.0 regression in `/orders/{id}/total`

## Root cause
`src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];` throws `KeyNotFoundException` for any tier missing from the table (`"standard"`, and any other tier not in the table), producing HTTP 500 on `GET /orders/{id}/total`. The 1.4.0 lookup table (commit `9bfbf7d`) contains only `"gold"` (0.05m) and `"silver"` (0.02m), silently dropping the 1.3.0 default of 0% discount for non-gold tiers.

## Change (one find-and-replace, one file)
File: `src/Orders.Api/Pricing.cs`, line 16.

Before:
```csharp
        var discount = TierDiscounts[customer.Tier];
```
After:
```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
```
Everything else in the file stays unchanged — the lookup table, the `"silver"` entry, and the 1.4.0 intended behavior are kept as-is.

## Why this is the smallest safe fix
- It's a single line in a single file; no changes to `OrderStore.cs`, `Program.cs`, or the tests.
- It restores the last good release's behavior for **every** input the regression broke, not just the standard-tier cases in this report: any tier not in the table gets 0% discount — exactly what 1.3.0 did (`"gold"` → 5%, everything else → 0%). Standard → 0% (1.3.0 behavior), unknown/typo'd tier → 0% (1.3.0 behavior), gold → 0.05m and silver → 0.02m (intended 1.4.0 behavior) unchanged.
- The alternative — adding `["standard"] = 0.00m` to the table — fixes only the known standard tier; any other missing tier would still throw and 500. `GetValueOrDefault` covers all missing tiers with the same 0% default and is a smaller edit.
- `GetValueOrDefault` uses the same ordinal string comparison as the indexer, so behavior for gold/silver is identical to today.

## Verification
Run `dotnet test` after the fix. It must show:
- `PricingTests.Standard_customers_pay_list_price_plus_gst` — pass
- `PricingTests.Totals_round_to_cents` — pass
- `OrdersApiTests.Order_totals_are_available(1001)` — pass
- All previously passing tests still pass: `Gold_customers_get_five_percent_off`, `Silver_customers_get_two_percent_off`, `Order_totals_are_available(1002)`, `Order_totals_are_available(1003)`, `Unknown_orders_return_404`
- Net: **8/8 passed, 0 failed**.

Note: the deployed 1.4.0 build on localhost will not change until a redeploy (out of scope), so verification is via `dotnet test`, not the live API.

## Risks
- **Unknown/mis-typed tiers now silently get 0% discount instead of failing.** E.g. a data-entry typo (`"glod"`) returns a 0%-discounted total rather than a 500. This is the faithful restoration of 1.3.0 semantics (non-gold → 0%), but it trades loud failure for silent acceptance. If strictness for unknown tiers is preferred, the stricter alternative is an explicit `"standard"` entry plus a hard failure for anything else — but that leaves every other missing tier broken, which the task explicitly rules out.
- **No test coverage for the unknown-tier path.** The existing suite only exercises standard/gold/silver, so the unknown-tier behavior of the fix is verified by code review, not by tests. Per the constraints, no new tests are planned.
- No behavior change for gold/silver/standard in the store; the `OrderStore.cs` change (customer 3 → silver) is unrelated and untouched.

**Not applied** — this is the plan only; per the task, no fix was written or committed.