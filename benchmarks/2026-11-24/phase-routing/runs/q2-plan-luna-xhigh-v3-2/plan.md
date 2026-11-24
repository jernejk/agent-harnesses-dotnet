## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The table at lines 7–11 includes `gold` and `silver`, but not `standard`. Order 1001 belongs to a standard-tier customer, so the lookup throws `KeyNotFoundException`, which causes the API test to receive HTTP 500.

The commit history confirms `9bfbf7d` replaced the previous “gold gets a discount, other tiers get zero” behavior with this strict lookup.

## Change (the exact lines before and after)

Before, `src/Orders.Api/Pricing.cs:16`:

```csharp
var discount = TierDiscounts[customer.Tier];
```

After:

```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

This is a one-line change that keeps the configured gold and silver discounts while returning zero for an unlisted tier. That restores the prior fallback behavior for standard customers without changing the pricing calculation or API handling. Existing tests already cover standard pricing, rounding, and order-total availability.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

The current baseline run reproduces the reported **3 failures and 5 passes**. After the change, it must show **8 passed, 0 failed**, including the standard-pricing, rounding, and order-1001 API tests. Do not use the live API for verification; the deployed 1.4.0 service will not reflect this repo change without a redeploy.

## Risks

Any tier absent from the lookup will receive zero discount. This matches the former fallback behavior; if unknown tiers should instead be rejected, that should be handled separately through tier validation. No files were changed.