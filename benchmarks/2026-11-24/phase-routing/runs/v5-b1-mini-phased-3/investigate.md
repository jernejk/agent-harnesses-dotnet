Symptom  
GET `/orders/{id}/total` returns HTTP 500 on the 1.4.0 deploy. I reproduced it locally with `GET /orders/1001/total -> 500`.

Reproduction  
- `GET /health` returns `200` with version `1.4.0`
- `GET /orders/1001/total` returns `500`

Change that caused it (commit and subject)  
- `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)  
- `src/Orders.Api/Pricing.cs:16`
- The code changed from a safe conditional:
  - `customer.Tier == "gold" ? 0.05m : 0m`
- to an indexed lookup:
  - `var discount = TierDiscounts[customer.Tier];`
- That throws a `KeyNotFoundException` for any tier not present in the table. Existing `standard` customers are still in the data set, and order `1001` belongs to a `standard` customer, so `/orders/1001/total` now crashes instead of returning a total.

Evidence  
- `git diff v1.3.0..HEAD` shows the pricing change in `src/Orders.Api/Pricing.cs`
- `git show 9bfbf7d -- src/Orders.Api/Pricing.cs` shows the exact replacement
- `git show v1.3.0:src/Orders.Api/Pricing.cs` confirms v1.3.0 handled non-gold tiers by giving them `0m` discount
- `git blame -L 13,18 src/Orders.Api/Pricing.cs` pins line 16 to commit `9bfbf7d`
- The local API reproduces the 500 on `/orders/1001/total`

Confidence  
High. The failing endpoint, the breaking commit, and the exact line causing the exception all line up.