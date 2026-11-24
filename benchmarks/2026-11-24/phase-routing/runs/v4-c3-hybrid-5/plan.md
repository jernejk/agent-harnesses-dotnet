## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes the discount table. The 1.3.0 conditional gave **every non-gold tier** a zero discount; the 1.4.0 lookup throws for tiers absent from the table, including `standard`. Order 1001 uses that tier, so its total request returns 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this one line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

The replacement restores the zero-discount fallback for all unrecognized tiers, including a runtime null, while retaining the 1.4.0 gold and silver discounts. Adding only `standard` to the table would leave other previously accepted tiers broken.

## Verification

After making the replacement, run `dotnet test`. All **8 tests must pass**, including:

- Standard pricing and rounding, plus the 1001 total endpoint’s HTTP 200.
- Gold and silver pricing, and HTTP 200 for their total endpoints.
- HTTP 404 for an unknown order.

The unmodified baseline produced 3 failures and 5 passes. No fix was applied or post-fix test run performed. Do not verify against the deployed localhost service: it will remain on 1.4.0 until redeployment.

## Risks

Existing tests do not cover unrecognized or null tiers, and the API tests assert status codes rather than response totals. The fallback addresses those tiers by construction, but the existing suite alone cannot demonstrate that coverage.