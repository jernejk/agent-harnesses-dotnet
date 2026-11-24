## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The 1.4.0 change replaced the safe default behavior for unknown tiers with a dictionary index lookup:

- Before: `customer.Tier == "gold" ? 0.05m : 0m`
- After: `var discount = TierDiscounts[customer.Tier];`

That fails for existing `"standard"` customers in `OrderStore`, because `TierDiscounts` only contains `"gold"` and `"silver"`. The indexer throws `KeyNotFoundException`, which becomes HTTP 500 on `GET /orders/{id}/total`.

## Change (the exact lines before and after)

Replace line 16 in `src/Orders.Api/Pricing.cs`.

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

- It changes only one line in one file.
- It restores the last good release’s behavior for every input the regression broke:
  - `"standard"` no longer throws and gets the intended default discount of `0m`.
  - Any other unknown tier also safely falls back to `0m` instead of crashing.
- It keeps the intended 1.4.0 behavior intact for known tiers:
  - `"gold"` still uses `0.05m`
  - `"silver"` still uses `0.02m`
- It avoids broadening the lookup table or changing pricing math elsewhere.

## Verification (what the existing tests must show after the fix)

After the change, `dotnet test` should pass all existing tests, specifically:

- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
- `Orders.Tests.PricingTests.Totals_round_to_cents`
- `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`

And no `KeyNotFoundException` should appear from `Pricing.Total`.

## Risks

- Any previously unhandled tier strings will now silently receive `0m` discount instead of failing fast. That matches the prior release behavior and fixes the regression, but it could mask bad data if unexpected tiers should be rejected.
- The fix assumes the intended default for unknown tiers remains no discount, which is consistent with 1.3.0 and the current tests.