## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table at lines 7–11 defines `gold` and `silver`, but not `standard`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs`), so the lookup throws `KeyNotFoundException` and the API returns 500.

This matches the regression in commit `9bfbf7d`: the previous code gave gold customers a discount and used zero for every other tier. I reproduced the reported failures: `dotnet test` currently has 3 failed and 5 passed.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`, replace:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It changes only the missing-key behavior: an unlisted tier receives the dictionary’s default discount of zero, preserving the previous non-gold fallback. The explicit gold and silver discounts remain unchanged.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It must show **8 passed, 0 failed**. In particular, the standard-tier pricing tests and `OrdersApiTests.Order_totals_are_available(orderId: 1001)` must pass; the latter asserts HTTP 200.

## Risks

Unlisted or misspelled tiers will receive zero discount rather than throw. That matches the pre-1.4.0 behavior, but it can make invalid tier data less visible.

I could not independently check `https://status.example.com`: the available command tool blocked the external request, and no HTTP-fetch tool was available. No fix was applied.