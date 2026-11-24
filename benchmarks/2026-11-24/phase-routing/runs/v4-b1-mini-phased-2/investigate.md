## Symptom
GET `/orders/{id}/total` returns HTTP 500 on the 1.4.0 deployment.  
The local API reproduces this: `/health` is 200 with version `1.4.0`, and `/orders/1001/total` returns 500.

## Reproduction
- `GET /health` → `200`
- `GET /orders/1001/total` → `500`

## Change that caused it (commit and subject)
- `9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**

This commit changed pricing from a conditional defaulting to `0m` into a dictionary lookup.

## Root cause (file:line and why)
- **`src/Orders.Api/Pricing.cs:16`**
- The code now does:
  ```csharp
  var discount = TierDiscounts[customer.Tier];
  ```
- That throws a `KeyNotFoundException` for any tier not present in the dictionary.
- In the current data, at least one customer/order path uses a tier that is not in the lookup, so `/orders/{id}/total` crashes and becomes HTTP 500.
- Before the change, the code handled unknown tiers safely:
  ```csharp
  var discount = customer.Tier == "gold" ? 0.05m : 0m;
  ```

## Evidence
- `git log v1.3.0..HEAD` shows the relevant change set landed before release 1.4.0.
- `git show 9bfbf7d -- src/Orders.Api/Pricing.cs` shows the unsafe lookup was introduced there.
- `git blame src/Orders.Api/Pricing.cs` attributes line 16 to `9bfbf7d`.
- `git diff v1.3.0..HEAD -- src/Orders.Api/OrderStore.cs src/Orders.Api/Pricing.cs src/Orders.Api/Program.cs` shows the new `silver` tier data path and the pricing lookup change.
- `GET /orders/1001/total` reproduces the 500 on the live local service.

## Confidence
High. The 500 is directly explained by the dictionary indexer in `Pricing.Total`, and the regression was introduced in the 1.4.0 change set.