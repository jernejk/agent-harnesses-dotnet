## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

`Pricing.Total` now looks up the customer discount with:

```csharp
var discount = TierDiscounts[customer.Tier];
```

But `TierDiscounts` only defines `"gold"` and `"silver"`. Existing customers in the data and tests still have tier `"standard"`, so the dictionary indexer throws `KeyNotFoundException`, which becomes HTTP 500 for `GET /orders/{id}/total`.

## Change (the exact lines before and after)

Replace this line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with this line:

```csharp
var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

- It changes only the failing lookup, in one file, with one find-and-replace.
- It restores the previous behavior for `"standard"` customers: no discount.
- It preserves the intended 1.4.0 behavior for known tiers:
  - `"gold"` still gets 5%
  - `"silver"` still gets 2%
- It avoids introducing new branching or test changes.
- It also protects any other unknown tier value from crashing the endpoint, which is safer than the current behavior.

## Verification (what the existing tests must show after the fix)

After the change, `dotnet test` should pass with the previously failing cases now succeeding:

- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
- `Orders.Tests.PricingTests.Totals_round_to_cents`
- `Orders.Tests.OrdersApiTests.Order_totals_are_available`

And there should be no remaining `KeyNotFoundException` from `Orders.Api.Pricing.Total` for tier `"standard"`.

## Risks

- Any previously unrecognized tier will now receive 0 discount instead of failing fast; this is acceptable here because it matches the last good release’s behavior for existing `"standard"` customers and prevents the regression.
- If the business later wants unknown tiers to be rejected, that would be a separate change with explicit validation, not part of this incident fix.