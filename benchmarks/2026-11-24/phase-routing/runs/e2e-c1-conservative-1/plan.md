## Root cause (file:line)

**Confirmed:** `src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table at lines 7–11 has entries for `"gold"` and `"silver"`, but not `"standard"`.

`src/Orders.Api/OrderStore.cs:9,14` maps order 1001 to customer 2, whose tier is `"standard"`. The API handler calls `Pricing.Total` in `src/Orders.Api/Program.cs:24`. The `9bfbf7d` patch changed the prior zero-discount fallback into a strict lookup without adding a `"standard"` entry.

This is reproduced by `dotnet test`: **3 failed, 5 passed**. The failures include the standard-pricing tests throwing `KeyNotFoundException` and the order 1001 API test receiving HTTP 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, change the table from:

```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

to:

```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["standard"] = 0m,
        ["silver"] = 0.02m,
    };
```

No other code change is needed for this incident.

## Why this is the smallest safe fix

The existing pricing tests specify that standard customers receive no discount, and the previous implementation also gave every non-gold tier a zero discount. Adding the explicit zero-discount entry restores that behavior while retaining the table lookup for supported tiers.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It should report **8 passed, 0 failed**, including the standard-pricing tests and `Order_totals_are_available(orderId: 1001)`. The test checks that the endpoint returns HTTP 200.

## Risks

The lookup will still throw for any tier not present in the table. That preserves strict handling of unknown tiers; any newly supported tier will need an explicit discount entry. No files were changed during this investigation.