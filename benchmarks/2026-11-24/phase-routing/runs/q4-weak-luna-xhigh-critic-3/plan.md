## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`. The 1.4.0 change in commit `9bfbf7d` replaced the prior “gold gets 5%, everyone else gets 0%” rule with direct dictionary indexing. The table contains `"gold"` and `"silver"` but not `"standard"`, so pricing a standard-tier customer throws `KeyNotFoundException`. That causes the two standard-tier pricing test failures and the HTTP 500 for order 1001.

The reported `Program.cs:30` problem-details helper is not the cause: it runs only on the unknown-order path, and `Program.cs:5` registers problem details. The existing unknown-order test passes.

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

This is one find-and-replace in one file. It keeps the 1.4.0 lookup table and applies its gold and silver discounts, while restoring the previous zero-discount fallback for every tier not in the table—not just `"standard"`. The null guard also avoids introducing an exception for a runtime null tier.

## Verification (what the existing tests must show after the fix)

Run `dotnet test` against the repo; do not use the live API, which remains on the deployed 1.4.0 build. The existing tests must show **8 passed, 0 failed**: standard-tier pricing and rounding pass, gold and silver discounts remain correct, all three known-order total requests return OK, and the unknown-order request still returns 404.

The current baseline is 3 failed and 5 passed; the failures are the two standard-tier pricing tests and the order-1001 API test.

## Risks

Treating an unmapped tier as zero discount preserves 1.3.0 behavior, but it also means an unexpected tier value won’t surface as an error. A new tier that should receive a discount must be added to the lookup table. No changes were applied.