## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table contains `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException` and the API returns 500.

The single `dotnet test` run confirmed the reported failures: both pricing failures throw for `standard` at this line, and the API total test receives `InternalServerError`. The 1.3.0 implementation applied a discount only to `gold`; all other tiers had no discount.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It changes only the lookup behavior: known tiers retain their configured discounts (`gold` and the newly intended `silver`), while missing tiers default to zero discount, matching 1.3.0’s behavior for every non-gold tier. The calculation and other 1.4.0 changes remain untouched.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`. All 8 existing tests should pass, including the two pricing tests that currently fail for `standard` and the API order-total test that currently gets HTTP 500. Do not add tests.

## Risks

`GetValueOrDefault` treats any unlisted tier as having no discount, as 1.3.0 did. If unlisted tiers should instead be rejected, that would be a separate behavior change.