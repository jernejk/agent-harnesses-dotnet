I have enough evidence now: the incident summary’s root cause is not supported by the code or stack trace. The real regression is the 1.4.0 pricing change that removed support for the `"standard"` tier, and that affects `/orders/{id}/total` for any standard customer, causing the 500. I’ll write the plan in the requested format and keep it to a single find-and-replace in `Pricing.cs`.
## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

The regression is here:

```csharp
var discount = TierDiscounts[customer.Tier];
```

In 1.4.0, `TierDiscounts` only contains `"gold"` and `"silver"`, so any customer with the pre-1.4.0 `"standard"` tier now throws `KeyNotFoundException` while computing `/orders/{id}/total`. That exception is what turns the endpoint into HTTP 500.

The incident finding about `Results.Problem(..., statusCode: 404)` is not the actual cause of the 500. The repo evidence shows that code path exists in `Program.cs`, but the failing test stack trace points to `Pricing.Total` instead, and `git blame` shows the missing `"standard"` tier was introduced in `Pricing.cs` by the 1.4.0 pricing change.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace the single lookup line.

**Before:**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After:**
```csharp
var discount = TierDiscounts.TryGetValue(customer.Tier, out var value) ? value : 0m;
```

## Why this is the smallest safe fix

- It is a one-line change in one file.
- It restores the last good behavior for every input the regression broke: unknown or previously-supported non-discount tiers no longer throw and instead receive no discount, which matches the pre-1.4.0 behavior for `"standard"`.
- It keeps the intended 1.4.0 changes intact:
  - `"gold"` still gets 5%
  - `"silver"` still gets 2%
- It does not alter order lookup, GST calculation, rounding, or API routing.
- It avoids reintroducing the old hardcoded tier logic while still preserving backward compatibility for existing customers.

## Verification (what the existing tests must show after the fix)

After the change, `dotnet test` should show:

- `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)` passes
- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst` passes
- `Orders.Tests.PricingTests.Totals_round_to_cents` passes
- overall test run is green

These are the existing tests that currently fail because the `"standard"` tier lookup throws.

## Risks

- If there are other customer tiers that should intentionally fail rather than default to 0% discount, this change will preserve availability but may mask bad data.
- This fixes the regression by restoring compatibility for unknown tiers; it does not add validation or telemetry for unexpected tier values.
- If the desired business rule in 1.4.0 was actually to remove `"standard"` entirely, then this fix intentionally keeps old behavior instead of enforcing the new restriction.