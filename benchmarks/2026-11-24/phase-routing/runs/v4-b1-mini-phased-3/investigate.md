Symptom
- GET `/orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.
- Local reproduction: `GET /orders/1001/total -> 500`.
- Tests fail in `Orders.Tests.PricingTests` and `Orders.Tests.OrdersApiTests`.

Reproduction
- `dotnet test` fails with:
  - `System.Collections.Generic.KeyNotFoundException: The given key 'standard' was not present in the dictionary.`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)` expecting `OK` but getting `InternalServerError`.
- The failing order is `1001`, whose customer tier is `standard`.

Change that caused it (commit and subject)
- Commit `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0`
- Subject: `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- The code changed from a safe conditional defaulting non-gold tiers to `0m`:
  - `var discount = customer.Tier == "gold" ? 0.05m : 0m;`
- to an indexing lookup:
  - `var discount = TierDiscounts[customer.Tier];`
- `TierDiscounts` only contains `gold` and `silver` at lines 7-11, but `OrderStore` includes a `standard` customer (`src/Orders.Api/OrderStore.cs:8`).
- So for `standard`, dictionary lookup throws `KeyNotFoundException`, which bubbles up as HTTP 500 from `/orders/{id}/total`.

Evidence
- `git log v1.3.0..HEAD` shows the relevant pricing change in `9bfbf7d`.
- `git show 9bfbf7d -- src/Orders.Api/Pricing.cs` confirms the exact diff.
- `git blame src/Orders.Api/Pricing.cs` attributes line 16 to `9bfbf7d8`.
- `dotnet test` reproduces the exception and the API 500.
- `src/Orders.Api/OrderStore.cs:8` shows customer `2` is `standard`, and `order 1001` belongs to customer `2`.

Confidence
- High. The failing stack trace, regression commit, and exact code path all line up with the observed 500.