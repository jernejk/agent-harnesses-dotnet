Symptom
- `GET /orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.
- Local reproduction: `GET /orders/1001/total -> 500`.

Reproduction
- Called the local API at `/orders/1001/total`.
- Response was `500`.
- The endpoint path exists and the failure happens during total calculation, not routing.

Change that caused it (commit and subject)
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`
- This is the change between `v1.3.0` and `v1.4.0` that introduced the regression.

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- The code changed from a safe conditional default:
  - old: `customer.Tier == "gold" ? 0.05m : 0m`
- To an unsafe dictionary index:
  - new: `var discount = TierDiscounts[customer.Tier];`
- The orders data includes a customer with tier `"standard"` (`src/Orders.Api/OrderStore.cs`), but `TierDiscounts` only contains `"gold"` and `"silver"`.
- For any `"standard"` customer, the dictionary indexer throws `KeyNotFoundException`, which bubbles up as HTTP 500 from `/orders/{id}/total`.

Evidence
- `git log --oneline v1.3.0..HEAD` shows:
  - `f1812c8 Release 1.4.0`
  - `ab38e53 Return problem details for unknown orders`
  - `9bfbf7d Move tier discounts into a lookup table and add silver tier`
- `git diff v1.3.0..v1.4.0 -- src/Orders.Api/Pricing.cs` shows the exact behavioral change to dictionary indexing.
- `git blame -L 7,17 src/Orders.Api/Pricing.cs` attributes the risky line to `9bfbf7d`.
- `file_access_grep` shows `"standard"` exists in `src/Orders.Api/OrderStore.cs`.
- `PricingTests` only cover standard behavior via unit tests, but the runtime code no longer handles `"standard"` safely.

Confidence
- High. The failure reproduces, the regression commit is identified, and the code path clearly explains the 500.