The fix is applied and the test suite passed, so I’ll summarize the verification.
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build, where `var discount = TierDiscounts[customer.Tier];` threw `KeyNotFoundException` for existing `standard` customers.
- Introduced by commit `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` with subject `Move tier discounts into a lookup table and add silver tier`.
- Fix: replace the unsafe dictionary indexer with a safe `TryGetValue` fallback to `0m`, preserving prior behavior for non-discounted tiers.
- Test result: `dotnet test` passed after the fix — 8 passed, 0 failed, 0 skipped.