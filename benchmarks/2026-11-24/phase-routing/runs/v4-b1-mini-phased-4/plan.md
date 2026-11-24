The tests confirm the regression and also show the intended 1.4.0 behavior: silver is new, but standard must still be treated as no discount. I have enough to write the implementation plan now, with exactly one change in `Pricing.cs`.
## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The regression is the direct dictionary index:

```csharp
var discount = TierDiscounts[customer.Tier];
```

In 1.4.0, `TierDiscounts` only contains `gold` and `silver`, but existing customers still use tier `standard`. That makes `/orders/{id}/total` throw `KeyNotFoundException` for normal orders, which becomes HTTP 500.

## Change (the exact lines before and after)

Replace the single failing line in `src/Orders.Api/Pricing.cs`.

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.TryGetValue(customer.Tier, out var value) ? value : 0m;
```

## Why this is the smallest safe fix

- It changes only one line in one file.
- It restores the pre-1.4.0 behavior for all tiers not explicitly in the lookup table, including `standard`.
- It keeps the intended 1.4.0 addition of the `silver` tier unchanged.
- It avoids reintroducing the old `if/else` logic, so the new lookup-table structure stays intact.
- It fixes the whole class of missing-tier crashes, not just the specific `standard` case in this report.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` and verify:

- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst` passes
- `Orders.Tests.PricingTests.Totals_round_to_cents` passes
- `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)` passes
- The full test suite is green

That confirms:
- `standard` no longer throws
- totals round correctly
- the API returns `200 OK` for order totals instead of `500`

## Risks

- Any future unlisted tier will now silently get `0m` discount instead of failing fast.
- If the business intended unknown tiers to be rejected, this would mask that problem; however, the evidence shows the regression is specifically about preserving existing `standard` behavior while keeping `silver`.
- No other code paths appear to depend on `Pricing.Total` throwing for missing tiers, so this is low risk and narrowly targeted.