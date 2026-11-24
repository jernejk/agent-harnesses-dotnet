### Symptom
Pricing for `standard` customers throws `KeyNotFoundException` for key `'standard'`. The order-total request for order 1001 consequently returns `InternalServerError`.

### Reproduction
Ran `dotnet test`: **3 failed, 5 passed**. Both standard-tier pricing tests failed, as did `OrdersApiTests.Order_totals_are_available(orderId: 1001)`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
[`src/Orders.Api/Pricing.cs:16`](src/Orders.Api/Pricing.cs#L16) indexes `TierDiscounts` directly with `customer.Tier`. The table contains only `gold` and `silver`, so a `standard` customer triggers the exception instead of receiving the zero discount used previously.

### Evidence
At v1.3.0, pricing used `customer.Tier == "gold" ? 0.05m : 0m`, which handled standard customers. The commit replaced that logic with the lookup; `git blame` attributes line 16 to `9bfbf7d`.

### Confidence
High. The failing tests, diff from v1.3.0, and blame all point to the same regression.