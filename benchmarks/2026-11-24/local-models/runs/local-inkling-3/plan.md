The findings check out: the diff, the failing tests, and the code all agree. I've now read the exact line I'll plan to change (`src/Orders.Api/Pricing.cs:16`) and confirmed the `TierDiscounts` table is only used there. Here is the plan.

## Plan: restore last-good total behavior for non-gold/silver tiers

**Root cause (file:line)**
`src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];`. Commit `9bfbf7d` replaced the 1.3.0 expression `customer.Tier == "gold" ? 0.05m : 0m` with a lookup table containing only `["gold"]` (0.05m) and `["silver"]` (0.02m). `standard` was omitted, so any standard-tier order (e.g. 1001 → customer 2, Noah Patel) throws `KeyNotFoundException`, which the API surfaces as HTTP 500. Note this also breaks every *other* non-gold/silver tier (e.g. an unknown/typo'd tier string): in 1.3.0 those got 0%, in 1.4.0 they throw. The intended 1.4.0 changes are the silver tier (0.02m) and customer 3 re-tiered to `"silver"` — both must be kept.

**Change (the exact lines before and after)** — one find-and-replace in `src/Orders.Api/Pricing.cs`, line 16 only:

Before:
```csharp
        var discount = TierDiscounts[customer.Tier];
```
After:
```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
```

**Why this is the smallest safe fix**
- One find-and-replace in one file, exactly as constrained; no new tests, no other edits.
- Gold (0.05m) and silver (0.02m) entries are untouched, so the intended 1.4.0 behavior is preserved.
- Every input the regression broke now behaves exactly as 1.3.0 did: any tier not in the table (standard, and any unknown tier) gets 0% discount — the same as 1.3.0's non-gold → 0m — instead of a 500. A standard-entry-only fix would leave unknown tiers still throwing, which is not "restore the last good release's behaviour for every input the regression broke."
- Verified against the existing tests: standard cases (110.00, 21.89) now compute 0% → `100.00 * 1.10 = 110.00` and `19.90 * 1.10 = 21.89`; gold (104.50) and silver (107.80) are unchanged.

**Verification**
Run `dotnet test` (not the live API — a repo change does not reach the running 1.4.0 build without a redeploy, which is out of scope). After the fix, all 8 tests must pass (0 failed): the 3 currently failing tests — `PricingTests.Totals_round_to_cents`, `PricingTests.Standard_customers_pay_list_price_plus_gst`, `OrdersApiTests.Order_totals_are_available(1001)` — plus the 5 already-passing tests, including the silver-tier and gold-tier cases and the 404 case for unknown orders. No new tests should be added.

**Risks**
- An unknown/mis-tiered customer now silently gets 0% (rather than 500ing). This is the faithful restoration of 1.3.0 behavior, but it can mask a data-entry error — e.g. a tier spelled `"gold "` or `"Gold"` would compute at 0% instead of failing loudly. If loud failure on unknown tiers is preferred, the alternative single find-and-replace is to keep `TierDiscounts[customer.Tier]` and add an explicit `["standard"] = 0.00m,` entry to the table — but that alone does not restore last-good behavior for unknown tiers, so it is not the recommended plan.
- Tier strings are case-sensitive; this is unchanged from 1.3.0 (only exact `"gold"` matched), so it is not a regression.
- No other callers of `TierDiscounts` exist (verified by grep), so the change is contained to this one line.
- The fix does not touch `OrderStore.cs`, the silver re-tier, or the API layer; the deployed 1.4.0 service will not reflect it until a redeploy (out of scope).

Per the investigation-phase instructions, I have not written or applied this fix — it is the plan for the engineer to implement.