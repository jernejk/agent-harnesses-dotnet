## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The 1.4.0 table contains only `gold` and `silver`, so `standard`—and any other tier absent from the table—throws `KeyNotFoundException`.

The findings are supported by the code and the 1.4.0 commit diff. The parent commit confirms the previous behavior: only exact `"gold"` received a discount; every other tier received zero. The current tests and seeded order 1001 reproduce the issue.

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

It changes one line in one file. The lookup still applies the existing 5% gold and 2% silver discounts, while absent tiers get zero discount, matching 1.3.0’s behavior for every non-gold tier. It also handles a null tier without throwing. Adding only a `standard` entry would fix the reported case but leave other absent tiers throwing.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should show **8 passed, 0 failed**, including:

- Standard-tier pricing and cent-rounding tests pass.
- Gold and silver discount tests still pass.
- API total requests for orders 1001, 1002, and 1003 return OK.
- The unknown-order test still returns 404.

The baseline run before the fix had 3 failures and 5 passes: the two standard-tier pricing tests and order 1001’s API test failed.

## Risks

Unknown or misspelled tiers will continue to receive zero discount, as they did in 1.3.0; this fallback could conceal an unintended tier value. I did not modify files or add tests. Verification should use `dotnet test`, not the localhost API, which remains on the deployed 1.4.0 build until redeployed.