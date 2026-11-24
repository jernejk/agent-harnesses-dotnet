The proposal has been written.

**Summary:**
- Root cause confirmed in `src/Orders.Api/Pricing.cs`: the `TierDiscounts` dictionary (lines 7–11) only has `gold` and `silver`, so line 16's `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for standard-tier customers (order 1001 / customer 2), producing HTTP 500.
- Proposal `fix-tier-discount-missing-key` (in `proposals/fix-tier-discount-missing-key.patch`): replaces line 16 `var discount = TierDiscounts[customer.Tier];` with `var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);`.

**Proposal name:** `fix-tier-discount-missing-key` — changes the discount lookup in `Pricing.cs` to a safe `GetValueOrDefault(customer.Tier, 0m)`, so standard/unlisted tiers price at 0% (1.3.0 behaviour) instead of throwing, while preserving the 1.4.0 gold (0.05) and silver (0.02) rates. The fix was not applied; a separate step applies and tests it.