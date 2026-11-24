## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table at lines 7–11 has entries for `gold` and `silver`, but not `standard`, so standard-tier orders throw `KeyNotFoundException`.

Order `1001` uses a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`). Commit `9bfbf7d` introduced the lookup and removed the prior zero-discount fallback for non-gold tiers.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier to the dictionary.

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

## Why this is the smallest safe fix

It adds one entry for a valid tier that should receive no discount. It preserves the lookup and leaves gold and silver pricing unchanged, while continuing to fail fast for unrecognized tier values rather than silently treating them as standard.

## Verification (what to run and what it must show)

- Run `dotnet test` from the repository root. All 8 tests should pass, including the standard-pricing, rounding, and API order-total tests.
- Verify `GET /orders/1001/total` returns HTTP 200; its total should be `195.80 AUD`.

I couldn’t check `https://status.example.com`: the read-only command policy blocked the available `curl` command, and no `http_get` tool was available.

## Risks

Unrecognized tier strings will still throw, intentionally. No files were changed.