The `write_proposal` tool reported success and returned the diff it wrote (`proposals/fix-tier-discount-lookup.patch`), even though the `file_access_*` tools can't read it back (the proposal store is separate from the file-access store). Per the task instructions, I must not apply the patch — a separate step applies and tests it. The proposal is in place as required.

**Summary of what I did:**

- Read `src/Orders.Api/Pricing.cs` and confirmed line 16 (`var discount = TierDiscounts[customer.Tier];`) is the exact line the plan targets.
- Verified via grep that `TierDiscounts` is used only on that one line (the other match was a compiled DLL, irrelevant).
- Wrote one proposal, `fix-tier-discount-lookup`, with a single find-and-replace on line 16:
  - **Before:** `var discount = TierDiscounts[customer.Tier];`
  - **After:** `var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);`

This restores last-good (1.3.0) behavior for every input the 1.4.0 regression broke: any tier not in the table (standard and any unknown/typo'd tier) now gets 0% discount instead of throwing `KeyNotFoundException` (HTTP 500), while gold (0.05m) and silver (0.02m) entries — the intended 1.4.0 changes — are untouched. The patch was written, not applied, as required.

The proposal `fix-tier-discount-lookup` changes `src/Orders.Api/Pricing.cs` line 16: `TierDiscounts[customer.Tier]` → `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`, restoring 0% discount for tiers missing from the table (standard and unknown) instead of throwing HTTP 500, while keeping gold/silver unchanged.