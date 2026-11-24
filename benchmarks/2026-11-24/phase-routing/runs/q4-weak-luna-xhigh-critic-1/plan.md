## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`. Commit `9bfbf7d` changed the prior “gold gets 5%; every other tier gets no discount” rule to a dictionary indexer. The dictionary includes `"gold"` and the intended new `"silver"` tier, but not `"standard"`, so pricing a standard-tier order throws `KeyNotFoundException`. Order `1001` uses that tier, which explains its HTTP 500.

The reported problem-details cause is unsupported: `Program.cs:5` registers `AddProblemDetails()`, and the existing unknown-order test passes with 404. The reproduced 500 is for a known order and fails in `Pricing.Total`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16 only:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

This is one find-and-replace in one file. It retains the 1.4.0 lookup table and silver discount, while defaulting unlisted tiers to zero discount—the last-good behavior for non-gold tiers. The null guard also avoids introducing an exception for a null tier.

## Verification (what the existing tests must show after the fix)

The current `dotnet test` baseline is **3 failed, 5 passed**. After the change, run `dotnet test`; the existing suite should show **8 passed, 0 failed**, including:

- Standard-tier pricing and cent-rounding tests passing.
- Gold and silver pricing tests still passing.
- API totals for orders `1001`, `1002`, and `1003` returning 200.
- Unknown orders still returning 404.

I did not apply the fix, so these are expected post-change results, not a test run against the modified code.

## Risks

Unlisted tiers will now be treated as zero-discount instead of throwing. That matches the previous behavior but could conceal invalid tier data. The existing tests cover standard, gold, and silver, but not arbitrary or null tiers; no new tests are planned. The `Program.cs` problem-details behavior remains unchanged.