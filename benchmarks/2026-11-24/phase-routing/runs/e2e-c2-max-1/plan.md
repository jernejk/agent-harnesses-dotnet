### Root cause (file:line)

Confirmed. `src/Orders.Api/Pricing.cs:7–11` defines discounts for `"gold"` and `"silver"` only, and line 16 indexes the dictionary with `customer.Tier`. For a standard customer, that lookup throws `KeyNotFoundException`.

The path matches the report: `src/Orders.Api/OrderStore.cs:8,14` maps order 1001 to a standard-tier customer, and `src/Orders.Api/Program.cs:24` passes that customer to `Pricing.Total`. Running `dotnet test` reproduced the two pricing failures and the order-1001 API failure.

Commit `9bfbf7d` introduced the lookup and replaced the prior behavior—gold gets 5% off; other tiers get no discount—with the incomplete table lookup.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add one entry before the dictionary’s closing `};`:

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

Leave the lookup at line 16 unchanged.

### Why this is the smallest safe fix

It adds the missing supported tier with the no-discount behavior expected for standard customers, without changing pricing calculations or endpoint behavior. Existing tests already cover standard pricing, rounding, and the API request for order 1001; no test changes are needed.

### Verification

After the change, run `dotnet test`. It must show all 8 tests passing, including the standard pricing and rounding tests, the order-1001 API test, and the gold/silver tests.

### Risks

An unrecognized future tier will still throw at the dictionary lookup. That retains the table’s explicit-tier behavior; any new supported tier will need its own mapping. No files were changed during this investigation.