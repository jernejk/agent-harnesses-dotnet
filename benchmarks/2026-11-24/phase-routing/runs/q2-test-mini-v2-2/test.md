I found the applied fix, but the API process appears to still be serving the old build.
- Root cause: `src/Orders.Api/Pricing.cs:16` used `TierDiscounts[customer.Tier]`, so `standard` customers were missing from the lookup and threw `KeyNotFoundException`.
- Commit that introduced it: `9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.
- Fix: changed the lookup to `TierDiscounts.GetValueOrDefault(customer.Tier)`, restoring the previous zero-discount fallback for unlisted tiers.
- Test result after the fix: `dotnet test` passed (`8 passed, 0 failed`), but `GET /orders/1001/total` still returned `500`, indicating the running API instance is still on the old build or otherwise not serving the updated code.