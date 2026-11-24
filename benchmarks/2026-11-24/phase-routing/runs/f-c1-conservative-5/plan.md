## Root cause (file:line)

**Confirmed:** `src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`. Order 1001 uses customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the indexer throws `KeyNotFoundException`. The total endpoint calls `Pricing.Total` at `src/Orders.Api/Program.cs:24` without catching that exception.

This matches the history: commit `9bfbf7d` replaced the previous “gold gets 5%, all other tiers get no discount” logic with the strict dictionary lookup. `git blame` attributes the failing line to that commit.

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

It changes one line and restores the prior behavior for tiers absent from the dictionary: they receive no discount. The existing gold and silver rates remain unchanged. The current tests already cover standard pricing, rounding, and totals for orders 1001–1003, so no test changes are needed for this fix.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It must pass all 8 tests, including the standard-tier pricing and rounding tests and the endpoint theory for orders 1001, 1002, and 1003. The pre-fix run reproduced 3 failures and 5 passes, including the `standard` key exception and the 500 for order 1001.

Do not use the localhost API as verification: it is still the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

An unrecognized tier will continue to receive no discount, matching the previous behavior, but this can mask a misspelled or unexpected tier value. If unknown tiers should instead be rejected, that would be a separate behavior change. No files were modified.