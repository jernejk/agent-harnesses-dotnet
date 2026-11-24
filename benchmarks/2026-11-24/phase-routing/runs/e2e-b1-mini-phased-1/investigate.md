Completed the investigation and marked the todo done.

Symptom
- `GET /orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.

Reproduction
- Local API call: `GET /orders/1001/total` -> `500`
- `dotnet test` fails with `KeyNotFoundException` for tier `standard`

Change that caused it (commit and subject)
- `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0`
- `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)
- `src/Orders.Api/Pricing.cs:16`
- `var discount = TierDiscounts[customer.Tier];`
- The new lookup table only contains `gold` and `silver`, but the app still has `standard` customers/orders, so the dictionary access throws `KeyNotFoundException` and the API returns 500.

Evidence
- `git diff v1.3.0..v1.4.0 -- src/Orders.Api/Pricing.cs src/Orders.Api/OrderStore.cs`
- `git blame -L 1,80 src/Orders.Api/Pricing.cs`
- `dotnet test` stack trace points to `Pricing.Total` line 16
- `OrderStore.cs` still contains a `standard` customer used by order `1001`

Confidence
- High