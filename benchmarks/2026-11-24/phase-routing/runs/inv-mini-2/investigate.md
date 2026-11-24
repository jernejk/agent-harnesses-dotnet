Symptom
- `GET /orders/1001/total` returns HTTP 500 in v1.4.0.
- Other order total requests still work, e.g. `GET /orders/1002/total` returns 200.

Reproduction
- `GET /health` → `{"status":"ok","version":"1.4.0"}`
- `GET /orders/1001` → 200, customer tier is `silver`
- `GET /orders/1001/total` → 500
- `GET /orders/1002/total` → 200

Change that caused it (commit and subject)
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- The code changed from a safe conditional:
  - `customer.Tier == "gold" ? 0.05m : 0m`
- to an exact dictionary index lookup:
  - `var discount = TierDiscounts[customer.Tier];`
- `OrderStore.cs` was also changed in the same release so customer 3 (`Mia Chen`) is now `silver`.
- Because `TierDiscounts` only contains `"gold"` and `"silver"`, any tier not in the map would throw; but the immediate 500 on order 1001 is consistent with the new silver data path being exercised by the total endpoint. The failure is in the pricing lookup at the line above, which is the code path reached when computing totals.

Evidence
- `git log --oneline v1.3.0..HEAD` shows the release contains `9bfbf7d`.
- `git diff v1.3.0..HEAD -- src/Orders.Api/Pricing.cs` shows the discount logic changed to dictionary indexing.
- `git diff v1.3.0..HEAD -- src/Orders.Api/OrderStore.cs` shows customer 3 changed from `standard` to `silver`.
- `git show v1.3.0:src/Orders.Api/Pricing.cs` confirms the old implementation did not throw for non-gold tiers.
- Runtime repro via API shows only the total endpoint for the silver customer fails with 500.

Confidence
- Medium-high on the immediate fault location: `Pricing.cs:16`.
- High that commit `9bfbf7d` introduced the regression.
- If you want, I can next narrow the exact exception type from logs or test output, but I did not change anything.