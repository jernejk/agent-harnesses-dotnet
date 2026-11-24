## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` using the customer’s tier. The table contains `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`).

I reproduced the reported test result: **3 failed, 5 passed**.

## Change (the exact lines before and after)

At `src/Orders.Api/Pricing.cs:16`:

**Before**
```cs
var discount = TierDiscounts[customer.Tier];
```

**After**
```cs
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It is a one-line change that keeps the gold and silver discounts while restoring the prior zero-discount fallback for tiers not in the table. That gives standard customers list price plus GST without changing the API route or order data.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It must show **8 passed, 0 failed**, including both standard-tier pricing tests and the API test for order 1001. The API test should receive HTTP 200; the order total should be **195.80 AUD**.

## Risks

- Unmapped or misspelled tiers will receive a zero discount instead of failing fast. This matches the pre-1.4.0 fallback behavior; confirm that behavior remains intended.
- I could not verify `https://status.example.com`: the available command tool blocked the network request, and no HTTP-fetch tool was available. No status-page incident state is inferred.
- No source changes were made.