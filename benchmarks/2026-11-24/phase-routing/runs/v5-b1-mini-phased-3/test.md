I’ve confirmed the repo state and test result, so I can summarize the incident resolution.
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 code path indexed `TierDiscounts[customer.Tier]`, which throws `KeyNotFoundException` for existing `standard` customers and causes `GET /orders/{id}/total` to return HTTP 500.
- Introduced by commit: `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: applied `fix-order-total-tier-fallback`, changing pricing to use `TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m` so unknown/non-discounted tiers fall back safely instead of crashing.
- Test result: `dotnet test` passed — `8` tests passed, `0` failed.