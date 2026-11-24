Status-page check: **I couldn’t verify `https://status.example.com`**. The available read-only command interface denied the HTTP request, and no alternate HTTP-fetch tool is available here. The diagnosis below is verified against the repository.

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` looks up every customer tier directly in `TierDiscounts`. That table contains `gold` and `silver`, but not `standard`. For order 1001, the standard-tier lookup throws `KeyNotFoundException`, which causes the API to return HTTP 500.

The 1.4.0 change replaced the prior behavior—5% off for gold and zero discount for every other tier—with this direct lookup. Running `dotnet test` reproduced the reported three failures: two pricing tests and the API test for order 1001.

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

For a missing key, `GetValueOrDefault` returns the default value for `decimal`, which is zero. This restores the previous no-discount fallback for standard and other unlisted tiers, while retaining the configured discounts for gold and silver. No API or order-data changes are needed.

## Verification (what to run and what it must show)

- Run `dotnet test`. It must pass all 8 tests, including the standard-tier pricing, rounding, and order-total API tests.
- Verify `GET /orders/1001/total` returns HTTP 200. The expected total is 195.80 AUD (order subtotal 178.00, with GST and no standard-tier discount).

The current baseline run produced 3 failures and 5 passes; no fix was applied.

## Risks

Unknown or misspelled tiers will continue to receive no discount rather than fail loudly. This matches the pre-1.4.0 behavior, but could conceal bad tier data if unknown tiers should instead be rejected.