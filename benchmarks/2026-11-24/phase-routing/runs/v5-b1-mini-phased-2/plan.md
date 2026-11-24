## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

`Pricing.Total` now does:

```csharp
var discount = TierDiscounts[customer.Tier];
```

That lookup table only has `"gold"` and `"silver"`. Existing customers can still have tier `"standard"`, so `/orders/{id}/total` throws `KeyNotFoundException` for standard customers and the API returns HTTP 500.

## Change (the exact lines before and after)

Replace the lookup at line 16 with a safe default for unknown/non-discount tiers, preserving the 1.4.0 gold/silver discounts:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
```

## Why this is the smallest safe fix

- It changes only one line in one file.
- It restores the pre-1.4.0 behavior for `"standard"` customers: no discount, list price plus GST.
- It keeps the intended 1.4.0 behavior for `"gold"` and `"silver"` customers.
- It avoids reintroducing the old hardcoded conditional while still preventing the dictionary exception for any tier not in the table.
- It fixes the broader regression class, not only the exact reported order id, because every standard customer now follows the old safe path.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` and confirm all existing tests pass, especially:

- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
- `Orders.Tests.PricingTests.Totals_round_to_cents`
- `Orders.Tests.OrdersApiTests.Order_totals_are_available`

The expected outcome is that the previous `KeyNotFoundException` disappears and `/orders/{id}/total` returns `200 OK` again for standard customers.

## Risks

- Any tier not present in the lookup table will now receive `0m` discount instead of failing. That is the intended safe fallback for the current contract, but it may hide future missing-tier data issues.
- If the business later wants unknown tiers to fail loudly, this fallback would need revisiting.