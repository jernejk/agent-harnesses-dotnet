I've completed the investigation. Everything in the findings checks out: the tests fail exactly as reported, the diff confirms the root cause, and the 1.3.0-era behavior is visible in the pre-1.4.0 ternary. No files were changed. Here is the plan.

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];`

The `TierDiscounts` dictionary (lines 7–11) is seeded with only `"gold"` (0.05m) and `"silver"` (0.02m). Commit `9bfbf7d` replaced the 1.3.0 ternary `customer.Tier == "gold" ? 0.05m : 0m` — which treated every non-gold tier as 0% — with this dictionary index, dropping the implicit "standard"/default-0% handling. Standard-tier customers (order 1001 → customer 2, tier `"standard"`) throw `KeyNotFoundException: 'standard'`, which the `/orders/{id}/total` endpoint (`Program.cs`) does not handle → HTTP 500. The regression broke every non-gold, non-silver tier, not just standard.

## Change (exact lines before and after)

One find-and-replace, one file: `src/Orders.Api/Pricing.cs`, line 16.

**Before:**
```csharp
        var discount = TierDiscounts[customer.Tier];
```
**After:**
```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
```

Everything else in the file stays as-is: the `TierDiscounts` table (with the intended 1.4.0 silver entry), the GST rate, the rounding logic, and the `OrderStore.cs` silver customer change.

## Why this is the smallest safe fix

- **One line, one file** — exactly the "one find-and-replace" budget. No new code paths, no new tests, no endpoint changes.
- **Restores 1.3.0 behavior for every input the regression broke**, not just the reported case: gold → 0.05m (intended), silver → 0.02m (intended 1.4.0 change), standard and any other/unknown tier → 0m via the default — exactly the old `: 0m` branch semantics. A dictionary index that throws on anything unlisted would still 500 on an unlisted tier.
- **Keeps the intended 1.4.0 changes**: the lookup table, the silver tier (customer 3 / order 1003 → 2% off), the silver test, the problem-details handling (`ab38e53`), and the version bump (`f1812c8`) are all untouched.
- `GetValueOrDefault(key, 0m)` is a well-understood, allocation-free dictionary lookup; the default `0m` is precisely the old `: 0m` branch.
- Note on the alternative: adding `["standard"] = 0.00m` to the dictionary would fix the reported symptom but leave any other unlisted tier throwing — it does not satisfy "every input the regression broke." The `GetValueOrDefault` approach is the one that does. (If the team deliberately wants unknown tiers to fail loudly, that's a different decision — it would not be a restoration of the last good release.)

## Verification (what the existing tests must show after the fix)

Run `dotnet test` (the live 1.4.0 build on localhost will not pick up the fix until a redeploy, which is out of scope — tests are the verification vehicle). Expected after the fix:

- `PricingTests.Standard_customers_pay_list_price_plus_gst` — passes (110.00m).
- `PricingTests.Totals_round_to_cents` — passes (21.89m).
- `OrdersApiTests.Order_totals_are_available(orderId: 1001)` — passes (OK, was InternalServerError).
- Still passing (unchanged): `Gold_customers_get_five_percent_off`, `Silver_customers_get_two_percent_off`, `Order_totals_are_available` for 1002 and 1003, and `Unknown_orders_return_404`.
- Net: 8/8 passing, up from 5 passed / 3 failed.

## Risks

- **Silent 0% for unknown tiers** — an unlisted tier string now gets 0% instead of a 500. That is the intended 1.3.0 behavior (the old ternary did exactly this), but it means a typo'd tier (e.g. `"Gold"`) would silently yield no discount rather than fail loudly. Same trade-off the last good release made; flag it to the team, don't "fix" it beyond the restore.
- **No new tests planned** (per the task) — the existing tests cover standard (2), gold, silver, and the 3 orders through the API; they do not cover a hypothetical unknown tier. That is acceptable given the instruction not to add tests, but the silent-default risk above rests on that gap.
- **Deployment** — the running localhost service stays 1.4.0 (500 on standard-tier orders) until the fix is redeployed; verification via `dotnet test` only, not the live API.
- **Out of scope / unchanged** — the problem-details change (`ab38e53`) and version bump (`f1812c8`) are unrelated and must not be touched by this fix.

No fix was applied and no files were modified — this is the plan for the engineer who implements it.