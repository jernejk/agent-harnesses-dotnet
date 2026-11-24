### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table contains `gold` and `silver`, but not `standard`. Order 1001 belongs to a standard-tier customer, so this lookup throws `KeyNotFoundException`; the `/orders/{id}/total` route calls `Pricing.Total`, resulting in HTTP 500.

I confirmed the cited commit replaced the previous “gold gets 5%, otherwise zero” behavior with this direct lookup. The current `dotnet test` run reproduces the reported 3 failures and 5 passes.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, change only:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

### Why this is the smallest safe fix

It is a one-line change that restores the previous zero-discount fallback for tiers without a configured discount, while preserving the existing gold and silver discounts. It requires no changes to the order data, API route, or table.

### Verification (what to run and what it must show)

- Run `dotnet test`. It should report **8 passed, 0 failed**; in particular, standard pricing and rounding should pass, and the API test for order 1001 should receive HTTP 200.
- If checking the running API, `GET /orders/1001/total` should return HTTP 200 with a standard-price total of **195.80 AUD**.

### Risks

- An unconfigured or misspelled tier will also default to zero discount. That matches the prior behavior, but it can conceal unexpected tier values.
- I could not verify `https://status.example.com`: the command policy denied `curl`, and no separate HTTP-fetch tool is available in this interface.
- No source changes were made; the working tree remains clean.