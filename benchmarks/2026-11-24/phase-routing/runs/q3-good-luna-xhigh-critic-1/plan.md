## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer’s tier. The table at lines 7–11 has entries for `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`.

This matches the order data: `src/Orders.Api/OrderStore.cs:14` maps order 1001 to customer 2, whose tier is `standard` at line 8. Commit `9bfbf7d` introduced the direct lookup, replacing the previous behavior that gave non-gold customers no discount. The API’s total handler passes this exception through as HTTP 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It changes only the failing lookup. Existing `gold` and `silver` discounts still come from the table; missing tiers receive the decimal default of `0`, restoring the prior fallback behavior without adding a special-case tier entry.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

The current baseline is **3 failed, 5 passed**. After the change, it should show **8 passed, 0 failed**. In particular, the standard-tier pricing and rounding tests should pass, and the API test for order 1001 should receive HTTP 200. I did not use the live API: the deployed 1.4.0 service would not reflect a repo patch without a redeploy.

## Risks

A misspelled or otherwise unrecognized tier will also receive no discount rather than raising an exception. That matches the prior fallback behavior; stricter validation would be a separate policy change.

No files were changed; the working tree is clean.