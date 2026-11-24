### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` using the customer’s tier. The dictionary at lines 7–11 contains only `gold` and `silver`; order 1001’s customer is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException` and the API returns HTTP 500.

The parent of commit `9bfbf7d` used a conditional that gave gold a discount and all other tiers zero discount. The commit replaced that fallback with the throwing lookup.

I could not verify `https://status.example.com`: the read-only command policy denied the attempted request and instructed me not to retry.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

### Why this is the smallest safe fix

It is a one-line change. A missing tier returns the decimal default, `0m`, restoring the previous no-discount fallback for standard and other unlisted tiers. The configured gold and silver discounts remain unchanged.

### Verification (what to run and what it must show)

Run:

```sh
dotnet test
```

It must show **8 passed, 0 failed**. In particular, the standard-pricing and rounding tests should pass, the API test for order 1001 should receive HTTP 200, and the existing gold, silver, and other API tests should continue to pass.

Before the change, `dotnet test` reproduced the reported result: **3 failed, 5 passed**.

### Risks

Unknown or misspelled tiers will receive zero discount rather than produce an error. That matches the prior behavior, but it could conceal invalid tier data; any new tier that needs a discount must be added explicitly to the lookup table.