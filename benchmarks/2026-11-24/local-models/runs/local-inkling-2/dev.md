The proposal was written successfully (the tool confirmed it and returned the diff). The `file_access_*` tools don't surface the proposals store, but that's expected — the separate apply step will consume it. I have not applied the change, per the task.

**Summary of what I did:**
- Read `src/Orders.Api/Pricing.cs` and confirmed line 16 is exactly the reported lookup: `var discount = TierDiscounts[customer.Tier];` with a table containing only `"gold"` (0.05m) and `"silver"` (0.02m) — no `"standard"` entry.
- Wrote the one-line proposal per the fix plan: replace the indexer with `TierDiscounts.GetValueOrDefault(customer.Tier, 0m);`, restoring 1.3.0's "non-gold → 0%" behavior for every missing tier while leaving gold/silver unchanged.

The proposal's name is **fix-tier-discount-lookup** and it changes `src/Orders.Api/Pricing.cs` line 16 — replacing the throwing dictionary indexer with `GetValueOrDefault(customer.Tier, 0m)` so any tier missing from the table (e.g. `"standard"`) gets a 0% discount instead of throwing `KeyNotFoundException` (HTTP 500).