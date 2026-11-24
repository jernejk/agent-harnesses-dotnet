## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table at lines 7–11 has `gold` and `silver` entries but no `standard` entry, so order 1001’s pricing calculation throws `KeyNotFoundException`.

The data and call path support the finding: `OrderStore.cs:8` assigns customer 2 the `standard` tier, `OrderStore.cs:14` assigns that customer to order 1001, and `Program.cs:24` calls `Pricing.Total` for the total endpoint. The cited commit `9bfbf7d` confirms that this lookup replaced the previous zero-discount fallback for non-gold customers.

## Change (the exact lines before and after)

Add one entry to the dictionary in `src/Orders.Api/Pricing.cs`:

**Before**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
```

**After**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
```

Leave the lookup and pricing calculation unchanged.

## Why this is the smallest safe fix

It explicitly restores the zero-discount behavior for the supported `standard` tier while preserving the existing `gold` and `silver` discounts. It is a one-line change and requires no endpoint or calculation changes. Existing tests already assert standard-tier pricing and include order 1001 in the API total test.

## Verification (what to run and what it must show)

Run `dotnet test`. It must pass all 8 tests, including the standard pricing tests and the API total test for order 1001.

In this checkout, I ran `dotnet test` before any fix: **3 failed, 5 passed**. The failures were the two standard-tier pricing tests and the order 1001 API total test, matching the missing-key cause. Do not use the live API for verification: the deployed 1.4.0 service will not receive a repo change without a redeploy, which is out of scope.

## Risks

This explicit entry handles the known `standard` tier, but other unlisted tier values would still throw at the dictionary lookup. That is a separate policy decision; the current seeded customers use only `gold`, `standard`, and `silver`.

No files were changed.