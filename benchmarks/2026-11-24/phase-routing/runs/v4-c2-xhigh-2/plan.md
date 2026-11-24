## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 contains only `gold` and `silver`, so a missing tier throws `KeyNotFoundException`.

That matches the incident path: `GET /orders/1001/total` reaches `Pricing.Total` via `Program.cs:24`; order 1001 belongs to customer 2, whose tier is `standard` (`OrderStore.cs:8,14`). The v1.3.0 implementation gave every non-`gold` tier no discount. The separate `ab38e53` commit only changes unknown-order 404 responses.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16 only:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is not null ? TierDiscounts.GetValueOrDefault(customer.Tier) : 0m;
```

## Why this is the smallest safe fix

This is one line in one file. The lookup returns the existing discount for `gold` and `silver`, and defaults missing tiers to zero—restoring the v1.3.0 behavior for `standard` and other non-table tiers without undoing the silver discount. The null guard also preserves the old conditional’s no-discount behavior for a null tier without passing a null key to the dictionary.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should report **8 passed, 0 failed**, including:

- Standard pricing and rounding expectations pass.
- Gold remains at 5% off and silver remains at 2% off.
- API total requests for orders 1001, 1002, and 1003 return OK.
- Unknown orders still return 404.

Before the fix, the run produced 3 failures and 5 passes. I did not apply the fix or run tests after it.

## Risks

The existing tests do not explicitly cover arbitrary unknown or null tiers; the proposed fallback handles them, but they are not directly verified by the current suite. The localhost service will still run the deployed 1.4.0 behavior until a redeploy, which is out of scope.