### Symptom
`GET /orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.

### Reproduction
- Local API call: `GET /orders/1001/total -> 500`
- Test run: `dotnet test` fails with:
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available` expected `OK`, got `InternalServerError`
  - `Orders.Tests.PricingTests.*` fail with `KeyNotFoundException: The given key 'standard' was not present in the dictionary.`

### Change that caused it (commit and subject)
- `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0`
- Subject: `Move tier discounts into a lookup table and add silver tier`

### Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- The code changed from a safe fallback:
  - old: `customer.Tier == "gold" ? 0.05m : 0m`
- to an indexed lookup:
  - new: `var discount = TierDiscounts[customer.Tier];`
- `TierDiscounts` only contains `"gold"` and `"silver"` at lines 7–10, but existing orders still include a `"standard"` customer (`src/Orders.Api/OrderStore.cs:8`).
- When `/orders/1001/total` resolves customer 2 (`"standard"`), the dictionary indexer throws `KeyNotFoundException`, which bubbles up as HTTP 500.

### Evidence
- `http_get /orders/1001/total` returned 500.
- `dotnet test` shows `KeyNotFoundException` from `Orders.Api.Pricing.Total` at `Pricing.cs:16`.
- `git show 9bfbf7d` shows the discount logic was changed in that commit.
- `git blame` confirms line 16 was introduced by `9bfbf7d`.
- `OrderStore.cs:8` still has a standard-tier customer used by order `1001` (`OrderStore.cs:14`).

### Confidence
High. The failing endpoint, exception, test failures, and the recent pricing change all point to the same root cause.