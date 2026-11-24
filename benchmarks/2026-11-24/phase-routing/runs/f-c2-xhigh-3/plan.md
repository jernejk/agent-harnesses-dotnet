### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using the customer’s tier, but the table at lines 7–11 has no `standard` entry. Order 1001 belongs to the standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException` and the API returns HTTP 500.

The commit history supports this cause: commit `9bfbf7d` introduced the lookup and replaced the previous conditional, which gave non-gold customers a zero discount. The current tests reproduce the issue: `dotnet test` fails the two standard-pricing tests and the order 1001 API test.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the missing zero-discount entry:

**Before**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

### Why this is the smallest safe fix

It restores the prior zero-discount behavior for standard customers while preserving the existing gold and silver rates and the lookup logic. The existing tests already cover standard pricing, rounding for a standard customer, and the order 1001 endpoint, so no additional test change is needed for this fix.

### Verification (what to run and what it must show)

Run `dotnet test` from the repository root. It must show all 8 tests passing, including both standard-pricing tests and the API test for order 1001. Do not use the live API to verify: the deployed 1.4.0 build will not receive this repository change without a redeploy.

### Risks

The lookup remains case-sensitive, and unsupported tier values will still throw rather than silently receive a zero discount. This fix handles the known `standard` tier; broader handling of unexpected tiers would be a separate change. No files were changed.