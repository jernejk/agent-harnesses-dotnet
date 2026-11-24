All findings are verified — the failures, diff, and code agree. I have everything needed to write the plan.

## Plan: restore 1.3.0 pricing behavior for non-gold tiers

**Root cause (file:line)** — `src/Orders.Api/Pricing.cs:16`
```csharp
var discount = TierDiscounts[customer.Tier];
```
The `TierDiscounts` table (added in commit `9bfbf7d`, 1.4.0) only contains `gold` (0.05m) and `silver` (0.02m). In 1.3.0 the logic was `customer.Tier == "gold" ? 0.05m : 0m`, which implicitly treated every non-gold tier — including `standard` — as 0% off. Any `standard`-tier customer (e.g. customer 2 / order 1001) now throws `KeyNotFoundException`, which the API surfaces as HTTP 500. Verified: `dotnet test` shows 3 failed / 5 passed (`PricingTests.Totals_round_to_cents`, `PricingTests.Standard_customers_pay_list_price_plus_gst`, `OrdersApiTests.Order_totals_are_available(1001)`), all pointing at Pricing.cs:16; the Program.cs problem-details change is unrelated.

**Change** — one find-and-replace in one file, `src/Orders.Api/Pricing.cs`, line 16:

Before:
```csharp
        var discount = TierDiscounts[customer.Tier];
```
After:
```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
```

**Why this is the smallest safe fix**
- It restores exactly the 1.3.0 semantics — non-gold tiers pay full list price (0% off) — for *every* input the regression broke, not just `standard`: any tier missing from the table (including future/unknown tiers) gets 0m, matching `tier == "gold" ? 0.05m : 0m`.
- It keeps the intended 1.4.0 changes: the lookup-table structure, the `silver` tier at 0.02m, and gold at 0.05m all stay.
- It's a single-line, single-file find-and-replace, with no new tests and no signature or behavior changes beyond the broken lookup.
- Alternative considered: adding `["standard"] = 0m,` to the table. That would fix the reported cases but leaves any other unknown tier throwing, so it does not fully restore 1.3.0 behavior. Rejected in favor of the safe lookup.

**Verification** — after the fix, `dotnet test` must show 8/8 passing (previously 3 failed / 5 passed). Specifically: `Standard_customers_pay_list_price_plus_gst` (110.00m), `Totals_round_to_cents` (21.89m), and `Order_totals_are_available(1001)` all pass; gold (104.50m) and silver (107.80m) pricing tests remain green; `Unknown_orders_return_404` still passes. Verification is via `dotnet test` only — the live 1.4.0 deployment is not touched by a repo patch.

**Risks**
- `GetValueOrDefault` silently applies 0% off for any tier not in the table (e.g. a typo'd tier string) rather than failing loudly. This is intentional — it mirrors the 1.3.0 behavior the fix restores — but if the team wants unknown tiers to be a hard error, that is a deliberate product decision, not a bug in this fix.
- No new tests are added (per the task constraint); the existing 8 tests cover the standard/gold/silver paths and the 1001/1002/1003 API cases.
- The live Orders service on localhost still runs the broken 1.4.0 build until a redeploy, which is out of scope; the fix will not be observable via the live API until then.
- The 1.4.0 Program.cs problem-details change is untouched and unrelated.