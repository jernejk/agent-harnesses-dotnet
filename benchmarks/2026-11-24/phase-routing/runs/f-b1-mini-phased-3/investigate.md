Finished — all investigation todo items are now complete.

Symptom  
- GET `/orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.

Reproduction  
- `GET /orders/1001/total -> 500`
- `GET /orders/1002/total -> 200`
- `GET /orders/1003/total -> 200`

Change that caused it (commit and subject)  
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)  
- `src/Orders.Api/Pricing.cs:16`
- `var discount = TierDiscounts[customer.Tier];`
- The lookup table only includes `"gold"` and `"silver"`, but customer `2` in `OrderStore` is still `"standard"`.
- For order `1001`, `customer.Tier` is `"standard"`, so the dictionary indexer throws `KeyNotFoundException`, which becomes the 500.

Evidence  
- `OrderStore.cs` lines 7–16 show customer `2` as `"standard"` and order `1001` belongs to that customer.
- `Pricing.cs` in v1.3.0 used a safe fallback for non-gold tiers.
- `git diff v1.3.0..HEAD` shows the pricing change that introduced the lookup table.
- The API route in `Program.cs` calls `Pricing.Total(order, customer)` directly for `/orders/{id}/total`.

Confidence  
- High. The repro, changed code, and missing tier entry all match the observed failure.