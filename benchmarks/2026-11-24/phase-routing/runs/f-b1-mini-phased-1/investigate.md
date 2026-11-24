Symptom  
GET `/orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.

Reproduction  
`GET /orders/1001/total` returns `500` in the current build.  
For comparison, `/orders/1003/total` and `/orders/1002/total` return `200`.

Change that caused it (commit and subject)  
`9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

Root cause (file:line and why)  
`src/Orders.Api/Pricing.cs:16`  
`var discount = TierDiscounts[customer.Tier];`  
This now indexes a dictionary that only contains `"gold"` and `"silver"`. Any customer whose tier is still `"standard"` will throw a `KeyNotFoundException` during total calculation, which surfaces as HTTP 500.

Evidence  
- `git log v1.3.0..HEAD` shows the relevant change set in 1.4.0.  
- `git diff v1.3.0..HEAD` shows `standard` customer data still exists in `OrderStore.cs` and only `Pricing.cs` changed from a safe ternary defaulting to `0m`.  
- `git show v1.3.0:src/Orders.Api/Pricing.cs` confirms 1.3.0 handled non-gold tiers safely.  
- `git blame src/Orders.Api/Pricing.cs` points the lookup-line change to `9bfbf7d`.  
- The failing endpoint `/orders/1001/total` uses customer 2, whose tier is `"standard"` in `src/Orders.Api/OrderStore.cs:8`.

Confidence  
High. The regression is directly explained by the code path and reproduced with the live endpoint.