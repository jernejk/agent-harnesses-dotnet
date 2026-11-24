All done. Here's the summary:

- **Root cause:** `src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];` threw `KeyNotFoundException` because the 1.4.0 `TierDiscounts` table only contained `["gold"]` and `["silver"]`, omitting `"standard"` (and any other unknown tier). Any standard-tier order (e.g. order 1001 → customer 2) therefore returned HTTP 500.
- **Introducing commit:** `9bfbf7d` — "Move tier discounts into a lookup table and add silver tier" (part of the 1.4.0 release, tagged `f1812c8` "Release 1.4.0").
- **Fix:** One find-and-replace on line 16 of `src/Orders.Api/Pricing.cs`: `TierDiscounts[customer.Tier]` → `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`. This restores 1.3.0 behavior for every tier missing from the table (standard and unknown) at 0% discount, while leaving the intended 1.4.0 gold (0.05m) and silver (0.02m) entries untouched.
- **Test result after the fix:** `dotnet test` → **Passed: 8, Failed: 0, Skipped: 0, Total: 8** — including the 3 previously failing tests (`PricingTests.Totals_round_to_cents`, `PricingTests.Standard_customers_pay_list_price_plus_gst`, `OrdersApiTests.Order_totals_are_available(1001)`).

Note: the fix is applied in the repo only; the live localhost service is still the deployed 1.4.0 build and will keep returning 500 for `/orders/1001/total` until a redeploy (out of scope).