## Root cause (file:line)

- `src/Orders.Api/Pricing.cs:16` directly indexes a dictionary that contains `gold` and `silver`, but not `standard` (`Pricing.cs:7–11`). The lookup throws `KeyNotFoundException`.
- Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`). Commit `9bfbf7d` replaced the previous zero-discount fallback for non-gold tiers with the direct lookup.

The baseline `dotnet test --no-restore` reproduced **3 failures and 5 passes**. I could not check the status page: external HTTP access is unavailable with the provided tools.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`, replace:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier != null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
            ? tierDiscount
            : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, replace the final lines:

```csharp
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
}
```

with:

```csharp
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));

    [Theory]
    [InlineData("bronze")]
    [InlineData(null)]
    public void Unknown_tiers_pay_list_price_plus_gst(string? tier) =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier!)));
}
```

## Why this is the smallest safe fix

The lookup retains the 1.4.0 gold and silver discounts while restoring the prior zero-discount fallback for standard, unknown, and null tier values. Adding only `standard` to the dictionary would leave the other fallback cases broken. The existing tests cover standard pricing, rounding, and the affected API; the added test protects the broader fallback.

## Verification (what to run and what it must show)

1. Run `dotnet test` after the change: all **10 test cases** must pass, including standard, gold, silver, rounding, unknown-tier, null-tier, and API tests.
2. Run the service and request `GET /orders/1001/total`: it must return HTTP 200 with order ID `1001`, total `195.80`, and currency `AUD`.
3. Check the status page through an authorized external channel; its current state could not be verified here.

## Risks

- Unknown or null tiers receive no discount rather than producing an error, matching the old fallback; invalid tier data could therefore go unnoticed.
- The fix has not been applied or tested after modification. The working tree remains clean.