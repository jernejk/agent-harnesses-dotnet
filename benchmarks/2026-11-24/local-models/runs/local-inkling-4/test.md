The verification is complete. The fix was applied via the proposal and the tests ran green.

**Root cause**: `src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];` indexed a dictionary seeded with only `"gold"` (0.05m) and `"silver"` (0.02m), so standard-tier customers (order 1001 → customer 2) threw `KeyNotFoundException: 'standard'` → HTTP 500.

**Introducing commit**: `9bfbf7d` — "Move tier discounts into a lookup table and add silver tier" (the 1.4.0 release commit `f1812c8` only bumps the version string; `ab38e53` is unrelated). The old 1.3.0 ternary `customer.Tier == "gold" ? 0.05m : 0m` implicitly treated every non-gold tier as 0%.

**Fix**: `src/Orders.Api/Pricing.cs:16` — changed `TierDiscounts[customer.Tier]` to `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`, restoring 1.3.0 behavior (any unlisted tier gets 0% discount) while keeping the intended 1.4.0 gold (5%) and silver (2%) entries. No other files touched.

**Test result after fix**: `dotnet test` → **Passed: 8, Failed: 0, Total: 8** (up from 5 passed / 3 failed). The live 1.4.0 build on localhost still returns 500 for standard-tier orders since the fix has not been redeployed — verification was via tests as expected.