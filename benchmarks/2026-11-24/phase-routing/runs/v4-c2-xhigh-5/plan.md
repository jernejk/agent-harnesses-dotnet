## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 contains only `gold` and `silver`, so a missing tier such as `standard` throws `KeyNotFoundException`. This matches the seeded customer for order `1001` and the observed test failure.

The finding is supported by the code and history: before commit `9bfbf7d`, any non-gold tier received no discount. The 1.4.0 change intentionally added a 2% silver discount, but changed the fallback behavior by indexing the table directly.

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

It is a single-line replacement in one file. Known tiers keep their table-defined discounts; any tier not in the table falls back to zero, restoring the previous behavior for all non-gold tiers rather than only adding a `standard` entry. The null check also preserves the old zero-discount behavior if a null tier is passed. The silver discount and lookup table remain intact.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests should pass, including:

- Standard pricing and rounding tests.
- Gold and silver pricing tests.
- The endpoint availability test for order `1001` as well as `1002` and `1003`.
- The unknown-order 404 test.

I ran `dotnet test` before any fix: it reports 3 failed and 5 passed, with the two standard-pricing failures and the order `1001` endpoint failure described above. No code was changed.

## Risks

The existing suite does not directly exercise arbitrary unknown tier strings or null tiers; the proposed fallback covers both. The running localhost API is still the deployed 1.4.0 build, so repository changes would not affect it without redeployment. Verification should therefore use `dotnet test`, not the live API.