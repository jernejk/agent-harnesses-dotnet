I have enough to stop investigating. I’ll write the implementation plan for the engineer with the exact one-line replacement, explain why it’s the smallest safe fix, and note the existing tests that must pass after the change.
Root cause (file:line)  
- `src/Orders.Api/Pricing.cs:16`
- The regression is this line:
  - `var discount = TierDiscounts[customer.Tier];`
- It now assumes every customer tier exists in the lookup table. Existing `standard` customers are not in the table, so `GET /orders/{id}/total` throws `KeyNotFoundException` and returns HTTP 500.

Change (the exact lines before and after)  
- **Before**:
  ```csharp
  var discount = TierDiscounts[customer.Tier];
  ```
- **After**:
  ```csharp
  var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
  ```

Why this is the smallest safe fix  
- It changes only the failing lookup in one file, so it is a true one-line fix.
- It restores the 1.3.0 behavior for all tiers not explicitly discounted: unknown or missing tiers now get `0m`, which matches the previous `customer.Tier == "gold" ? 0.05m : 0m` logic.
- It preserves the intended 1.4.0 change for the new discounted tiers:
  - `gold` still gets `0.05m`
  - `silver` still gets `0.02m`
- It avoids introducing new branching or broader refactors, and it prevents the exception for every input that lacks a dictionary entry, not just `standard`.

Verification (what the existing tests must show after the fix)  
- `dotnet test` should pass all existing tests.
- In particular, these previously failing tests must pass:
  - `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
  - `Orders.Tests.PricingTests.Totals_round_to_cents`
  - `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`
- The API test should no longer return `InternalServerError` for `/orders/1001/total`; the test suite is the verification source, not the live localhost service.

Risks  
- If any future tier was meant to fail hard instead of defaulting to no discount, this change would mask that. Based on the 1.3.0 behavior and the incident scope, defaulting to `0m` is the correct compatibility fix.
- If there are hidden tests expecting a different default for unrecognized tiers, they would conflict with the prior release behavior; the current evidence strongly suggests `0m` is the intended fallback.