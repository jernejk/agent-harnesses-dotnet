## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 contains only `"gold"` and `"silver"`. Order 1001 maps to a `"standard"` customer, so `Pricing.Total` throws `KeyNotFoundException`; the `/orders/{id}/total` handler then returns HTTP 500.

This was introduced by commit `9bfbf7d`. Before that change, only `"gold"` received a discount and every other tier defaulted to zero. The 1.4.0 silver discount is an intended change and should remain.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

It is a one-line change in one file. It keeps the lookup table and its 1.4.0 gold and silver rates, while restoring the previous zero-discount fallback for every unlisted tier. The null guard also preserves the old comparison’s behavior if a null tier is supplied at runtime. Adding only `"standard"` to the table would not restore the previous behavior for other unlisted tiers.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests must pass, including:

- Standard and gold pricing, silver’s 2% discount, and cent rounding.
- HTTP 200 for order totals 1001, 1002, and 1003.
- HTTP 404 for an unknown order.

The baseline run before any fix failed 3 tests and passed 5.

## Risks

The existing tests do not directly cover arbitrary unlisted tiers; the zero fallback is supported by the pre-1.4.0 implementation. The localhost service is still the deployed 1.4.0 build, so this repository change will not affect it without a redeploy. Verification should use `dotnet test`, not the live API.