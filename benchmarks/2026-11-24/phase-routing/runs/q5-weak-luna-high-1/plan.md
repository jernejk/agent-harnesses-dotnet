## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`. The tier lookup uses a dictionary indexer, which throws `KeyNotFoundException` for `"standard"` and any tier not in the dictionary. That exception causes the total endpoint to return HTTP 500.

The 1.3.0 implementation gave zero discount to every tier except `"gold"`. The 1.4.0 lookup-table change added the intended `"silver"` discount but lost that fallback. The reported `NotFound` diagnosis does not match the evidence: the failure is for an existing order, and `Program.cs` registers problem details.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

It is a single-line replacement in one file. It preserves the table’s gold and silver discounts, while restoring the prior zero-discount fallback for all other tier values—including a null value.

## Verification

Run the existing `dotnet test`. All 8 tests should pass, including the standard-tier pricing and rounding tests, the gold and silver discount tests, and `OrdersApiTests.Order_totals_are_available(orderId: 1001)`. Do not add tests or use the live API for verification.

## Risks

Unknown tiers will receive zero discount, matching the prior release’s behavior. The deployed 1.4.0 service will remain unchanged until redeployed; redeployment is out of scope.