Root cause (file:line)

- `src/Orders.Api/Pricing.cs:16`
- The 1.4.0 code uses `TierDiscounts[customer.Tier]`.
- `OrderStore.cs` still has customer `2` as `"standard"`, and order `1001` belongs to that customer.
- Because `"standard"` is not present in `TierDiscounts`, the dictionary indexer throws `KeyNotFoundException`, which surfaces as HTTP 500 on `GET /orders/1001/total`.

Change (the exact lines before and after)

- File: `src/Orders.Api/Pricing.cs`
- Before:
  ```csharp
  var discount = TierDiscounts[customer.Tier];
  ```
- After:
  ```csharp
  var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
      ? tierDiscount
      : 0m;
  ```

Why this is the smallest safe fix

- It changes only the failing lookup, leaving the route, data model, and pricing math unchanged.
- It restores the pre-1.4.0 behavior for customers whose tiers are not explicitly listed, which is exactly the regression seen for `"standard"`.
- It avoids introducing new pricing rules or touching unrelated endpoints.
- It preserves current behavior for `"gold"` and `"silver"` customers.

Verification (what to run and what it must show)

- Run:
  - `dotnet test`
- It must show:
  - all existing tests still pass
  - if there is an API/integration test for `/orders/{id}/total`, order `1001` should now return 200 instead of 500
  - if there is no such test yet, the important signal is that no test regresses after the fallback change

Risks

- If `"standard"` was supposed to receive a non-zero discount, this fallback would encode `0m` and may under-discount that tier.
- Any other unknown future tier will also receive `0m`, which is safe operationally but may hide missing configuration.
- This fix intentionally preserves old behavior rather than enforcing strict tier validation.