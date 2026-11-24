## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table has entries for `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`. The API then returns HTTP 500 for order 1001.

The history confirms the regression: before the lookup-table change, pricing applied a 5% discount to gold and zero discount to every other tier. The silver discount is an intended addition and should remain.

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

This changes only the failing lookup. Gold and silver continue to get the rates in the table; standard and any other unlisted tier get a zero discount, as they did before the regression. The null guard also preserves the old behavior for a null tier rather than allowing a dictionary lookup to throw. No changes to the API, table entries, or tests are needed.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. The existing suite should pass all 8 tests:

- Standard pricing is `110.00`; standard rounding remains `21.89`.
- Gold pricing remains `104.50`; silver remains `107.80`.
- Requests for orders 1001, 1002, and 1003 all return HTTP 200.
- The unknown-order request still returns HTTP 404.

I ran the suite before any change: it reproduced the report with 3 failures and 5 passes. I did not apply the fix, so post-fix results remain to be verified by the implementer.

## Risks

The existing tests do not explicitly cover null or arbitrary unlisted tier names; the proposed guard and fallback preserve their prior behavior by inspection. The API test checks response status, not the returned total. The deployed localhost service remains on 1.4.0 until redeployed, so it is not a verification target.