I have the verification result, and I’m ready to summarize the cause, commit, fix, and test outcome.
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build, where `TierDiscounts[customer.Tier]` threw `KeyNotFoundException` for existing `"standard"` customers, causing `GET /orders/{id}/total` to return HTTP 500.
- Introduced by commit `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed the tier lookup to `TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m`, preserving gold/silver discounts and defaulting unknown tiers to no discount.
- Test result: `dotnet test` passed after the fix; `8` tests passed, `0` failed.