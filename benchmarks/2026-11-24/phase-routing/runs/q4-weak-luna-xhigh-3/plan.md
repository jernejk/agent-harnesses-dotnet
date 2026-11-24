## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` — the 1.4.0 pricing refactor uses a dictionary indexer for `customer.Tier`, but the dictionary contains only `"gold"` and `"silver"`. Order `1001` belongs to a `"standard"` customer, so `Pricing.Total` throws `KeyNotFoundException`; the total endpoint returns HTTP 500.

The reported cause is not supported: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`. The `NotFound` helper is for unknown orders and is not reached for order `1001`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var tierDiscount) ? tierDiscount : 0m;
```

## Why this is the smallest safe fix

It is one line in one file. It preserves the 1.4.0 lookup table and its gold and silver discounts, while restoring the previous zero-discount fallback for any tier not in the table. The null guard also avoids a lookup exception for a null runtime value. No changes to the problem-details behavior, logging, or total calculation are needed.

## Verification (what the existing tests must show after the fix)

Run `dotnet test`—not the live API. It must pass all eight existing tests, including standard-tier pricing and totals for orders `1001`, `1002`, and `1003`; the unknown-order test must still receive 404. The current unmodified code reproduces three failures and five passes.

## Risks

The existing tests cover standard-tier fallback, but not arbitrary unrecognized tiers or a null runtime tier. The replacement preserves the old fallback for those values as well. The existing unknown-order test checks the 404 status, not the RFC 9457 response body.