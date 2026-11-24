## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`, but the dictionary at lines 7–11 has no `standard` entry. Order 1001 maps to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8`), so the lookup throws `KeyNotFoundException` and the endpoint returns 500.

The 1.4.0 change replaced the previous non-gold zero-discount fallback with this lookup. Existing pricing tests still require standard customers to pay list price plus GST.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add a zero-discount entry to the existing dictionary.

**Before:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It adds one entry for the already-used `standard` tier, restoring its existing zero-discount behavior without changing the endpoint, pricing formula, or gold and silver discounts. The current tests already cover standard pricing and the order-total endpoint, so no additional test change is needed for this fix.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

After the change, it should report **8 passed, 0 failed**. In particular, the standard pricing tests should pass, and the endpoint test for order 1001 should receive **200 OK**.

I ran `dotnet test` before any change: it reported **3 failed, 5 passed**, matching the incident. I did not edit any files or use the live API.

## Risks

The direct dictionary lookup will still throw for any tier not explicitly listed. This fix covers the existing `standard`, `gold`, and `silver` tiers; if unknown tiers should also receive zero discount, that requires a separate pricing-policy decision. The patch will not affect the deployed localhost service until it is redeployed.