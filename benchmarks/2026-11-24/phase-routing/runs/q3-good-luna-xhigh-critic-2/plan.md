### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 contains `"gold"` and `"silver"`, but not `"standard"`. `src/Orders.Api/OrderStore.cs:8,14` maps order 1001 to customer 2, whose tier is `"standard"`, so the lookup throws `KeyNotFoundException` and the endpoint returns 500.

The evidence supports the findings: commit `9bfbf7d` replaced the prior “gold gets 5%; other tiers get 0%” behavior with the direct lookup. Running `dotnet test` reproduced the reported result: **3 failed, 5 passed**.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

### Why this is the smallest safe fix

This is a one-line change that restores the pre-1.4.0 zero-discount fallback for unlisted tiers while retaining the lookup table for gold and silver discounts. Existing tests already cover standard pricing, rounding, and the order 1001 API request, so no additional test change is needed for this fix.

### Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

It must pass all **8 tests**, including the standard-tier pricing and rounding tests and the API test for order 1001. The current baseline is 3 failures and 5 passes. Do not use the live API for verification; the deployed 1.4.0 service will not contain this repository change without a redeploy, which is out of scope.

### Risks

The zero-default lookup also treats any other unlisted tier as zero-discount rather than throwing. That matches the prior implementation’s behavior. **No files were changed.**