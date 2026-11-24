Note on the "fix" part of your request: this is the investigation phase, so I'm reporting the root cause and not applying any change. Also, I could not open https://status.example.com — no internet/curl is available in this environment — so I relied on local tests, the API, and git.

## Findings

**Symptom**
`GET /orders/{id}/total` returns HTTP 500 since the 1.4.0 deploy. Tests fail: `Totals_round_to_cents`, `Standard_customers_pay_list_price_plus_gst`, and `Order_totals_are_available(1001)` (Order 1001 belongs to customer 2, tier "standard").

**Reproduction**
- `dotnet test` → 3 failed / 5 passed; errors are `KeyNotFoundException: The given key 'standard' was not present in the dictionary` at `src/Orders.Api/Pricing.cs:16`, plus an API 500 for order 1001.
- `GET /orders/1001/total` → `500` (also confirmed order 1003, customer 3, silver tier, presumably works — only 1001/standard fails; 1001 is the only standard-tier order and 1002 is gold).

**Change that caused it**
Commit `9bfbf7d` — "Move tier discounts into a lookup table and add silver tier" (part of 1.4.0; tag v1.3.0 predates it).

**Root cause**
`src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];`. The new `TierDiscounts` dictionary only contains `gold` (0.05m) and `silver` (0.02m); the previous `gold ? 0.05m : 0m` logic implicitly treated `standard` as 0% off. Any customer whose tier is `standard` (e.g. customer 2 / order 1001) now throws `KeyNotFoundException`, which the API surfaces as a 500. The fix would be to either add `["standard"] = 0m` to the table or use a safe lookup (e.g. `TryGetValue` defaulting to 0m) — but I have not applied it.

**Evidence**
- `git diff v1.3.0..HEAD -- src/Orders.Api/Pricing.cs` shows the dictionary added and the ternary replaced.
- `src/Orders.Api/OrderStore.cs` — customers 1 (gold), 2 (standard), 3 (silver); order 1001 → customer 2 (standard), 1002 → customer 1 (gold), 1003 → customer 3 (silver).
- Test failures: `PricingTests.cs:26` and `:14` both hit the missing `standard` key; `OrdersApiTests.Order_totals_are_available(1001)` gets `InternalServerError`.

**Confidence**
High — the failing stack trace points directly at `Pricing.cs:16`, the diff shows the lookup-table change, and the only standard-tier order (1001) is exactly what 500s.

One residual note: the API test only asserts 1001/1002/1003; the 1001 failure is the visible one, but any standard-tier customer would 500 the same way. The 1.4.0 `Program.cs` problem-details change is unrelated to this 500.