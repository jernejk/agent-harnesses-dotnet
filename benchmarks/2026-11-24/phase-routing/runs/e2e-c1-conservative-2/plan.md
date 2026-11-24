## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for the `standard` tier, which is absent from the dictionary at lines 7–11. Order 1001 belongs to customer 2, whose tier is `standard`; the total endpoint passes that customer to `Pricing.Total`.

The findings are supported by the code and history. Commit `9bfbf7d` replaced the previous zero-discount fallback for non-gold tiers with direct dictionary indexing. Running `dotnet test` reproduced the reported failures: **3 failed, 5 passed**, including HTTP 500 for order 1001.

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

It preserves the existing gold and silver discounts while restoring the prior zero-discount behavior for tiers absent from the table. That matches the existing standard-tier pricing tests without changing endpoint or pricing formulas.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It should report **8 passed, 0 failed**. In particular, standard-tier pricing should return `110.00` for a `$100` subtotal, rounding should still return `21.89`, and the API test for order 1001 should receive HTTP 200.

## Risks

Any tier not present in `TierDiscounts` will receive zero discount rather than fail. This matches the pre-1.4.0 fallback behavior; if unknown tiers should instead be rejected, that would require a separate policy change. No code was changed.