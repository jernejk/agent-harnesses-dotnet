The repo layout exposed by the file tools doesn’t include `src`/`tests`, so I’ll rely on the confirmed code paths from the readouts and keep the fix plan focused on the single failing line in `src/Orders.Api/Pricing.cs`.
## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The 1.4.0 change replaced the old tier check with:

```csharp
var discount = TierDiscounts[customer.Tier];
```

That throws `KeyNotFoundException` for the existing `"standard"` customer in `src/Orders.Api/OrderStore.cs:8`, because `TierDiscounts` only defines `"gold"` and `"silver"`. This breaks `GET /orders/{id}/total` for standard customers and causes the failing tests.

## Change (the exact lines before and after)

Replace the single lookup line in `src/Orders.Api/Pricing.cs`:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

No other lines should change.

## Why this is the smallest safe fix

- It is a one-line change in one file, matching the incident constraint.
- It restores the last good behavior for every unsupported tier by treating missing tiers as no discount, which is what 1.3.0 did.
- It keeps the intended 1.4.0 behavior for the new tiers already in the table: `"gold"` and `"silver"` still get their discounts.
- It avoids broadening the `TierDiscounts` table with a `"standard"` entry, which would be a larger behavioral change and would duplicate the old default case instead of restoring it generically.

## Verification (what the existing tests must show after the fix)

After the change, `dotnet test` should pass, including the tests that currently fail:

- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
- `Orders.Tests.PricingTests.Totals_round_to_cents`
- `Orders.Tests.OrdersApiTests.Order_totals_are_available`

The important observable result is that the `KeyNotFoundException` is gone and standard customers again receive a computed total instead of HTTP 500.

## Risks

- If `customer.Tier` is ever `null` or otherwise unexpected, `GetValueOrDefault` will return `0m`, which yields no discount. That matches the old fallback behavior, but it may hide bad data rather than surfacing it.
- This assumes the intended 1.4.0 behavior is “gold/silver get discounts, all other tiers get none,” which is supported by the prior code and the failing tests.