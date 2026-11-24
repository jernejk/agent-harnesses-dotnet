## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 contains only `gold` and `silver`, so pricing a `standard` customer throws `KeyNotFoundException`.

The finding’s fixture references are also correct: `src/Orders.Api/OrderStore.cs:8` assigns customer 2 the `standard` tier, and line 14 defines order 1001 for that customer. `src/Orders.Api/Program.cs:24` calls `Pricing.Total` without handling the exception, resulting in HTTP 500.

The history confirms commit `9bfbf7d8` replaced the previous zero-discount fallback for non-gold customers with the dictionary indexer. The cause reported in the investigation is supported by the code.

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

This is a one-line change. Missing tiers receive the decimal default of `0m`, restoring the prior non-gold fallback while preserving the configured `gold` and `silver` discounts. The project targets `net11.0`, so `GetValueOrDefault` is available.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It should pass all 8 tests. In particular, standard-customer pricing and the API totals test for order 1001 should pass; the API tests should continue to verify orders 1002 and 1003 return 200 and unknown order 9999 returns 404.

The current test run reproduces the reported failures: 3 failed, 5 passed.

## Risks

Any tier absent from the table will receive no discount. That matches the pre-1.4.0 behavior for non-gold tiers; rejecting unknown tiers instead would be a separate behavior change.

No code was changed.