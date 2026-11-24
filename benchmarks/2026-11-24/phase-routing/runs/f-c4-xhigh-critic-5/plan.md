### Root cause (file:line)

The findings are supported by the code. In `src/Orders.Api/Pricing.cs:7–11`, the discount table defines `gold` and `silver` but omits `standard`. At line 16, `Pricing.Total` directly indexes that table with `customer.Tier`, so a standard customer causes `KeyNotFoundException`.

Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`); the total endpoint calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`. The regression commit `9bfbf7d` replaced the previous “gold discount, otherwise zero” logic with the direct lookup. The existing pricing tests also expect standard customers to pay list price plus GST.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`, replace:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

### Why this is the smallest safe fix

It is a one-line change that keeps the gold and silver discounts while restoring the previous zero-discount behavior for tiers absent from the table. That covers `standard` without changing the endpoint or order data.

### Verification (what to run and what it must show)

From the repository root, run:

```text
dotnet test
```

It must pass all 8 tests, including the standard pricing tests and the API test that expects order 1001’s total endpoint to return HTTP 200. Before the fix, I ran `dotnet test`: it reproduced 3 failures and 5 passes, including the reported `KeyNotFoundException` and an `InternalServerError` for order 1001.

Do not use the live API as verification: the deployed 1.4.0 service will not receive this repository change without a redeploy.

### Risks

Any tier missing from the discount table will receive zero discount rather than fail fast. That matches the pre-1.4.0 behavior; if unknown tiers should instead be rejected, that would require separate validation and is outside this minimal fix.