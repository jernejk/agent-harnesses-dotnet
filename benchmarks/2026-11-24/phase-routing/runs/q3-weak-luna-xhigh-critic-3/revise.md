## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The dictionary at lines 7–11 contains only `"gold"` and `"silver"`, so `"standard"`—the tier assigned to the customer for order 1001 in `src/Orders.Api/OrderStore.cs:8,14`—throws `KeyNotFoundException`.

Commit `9bfbf7d` replaced the previous gold-only conditional with this lookup but omitted a fallback. Before that change, tiers other than gold received no discount. The `ProblemDetails` explanation is not the cause: `src/Orders.Api/Program.cs` registers `AddProblemDetails()`, and the `NotFound` helper handles unknown orders rather than successful totals.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, use a zero-discount fallback for any tier not in the table. This retains the configured gold and silver discounts while avoiding exceptions for unmapped tier strings.

**Before (line 16):**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After:**
```csharp
        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
            ? tierDiscount
            : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, the existing tests already cover standard, gold, and silver pricing. Add a test for an unmapped tier.

**Before (around lines 20–25):**
```csharp
    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));

    [Fact]
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
```

**After:**
```csharp
    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));

    [Fact]
    public void Unknown_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("platinum")));

    [Fact]
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
```

## Why this is the smallest safe fix

The fallback preserves the existing gold and silver rates and the prior behavior of charging no discount for tiers without a configured discount. It fixes `"standard"` without adding a special case for only that tier, and the regression test verifies the behavior for another unmapped tier. The existing standard, gold, and silver tests continue to verify known-tier pricing.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

Before the change, I ran this command: **3 failed, 5 passed**. The failures include both standard-pricing tests, which report `KeyNotFoundException` at `Pricing.cs:16`, and the order 1001 API test, which receives HTTP 500.

After the change and added test, the run should show **9 passed, 0 failed**. It must include the standard, gold, silver, and unmapped-tier pricing tests, the order 1001 totals test passing, and the unknown-order 404 test passing. Do not use the localhost API for verification: it remains the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

Unmapped tier strings will silently receive no discount. This is consistent with prior behavior and avoids granting an unintended discount, but a future policy may prefer rejecting invalid tiers at an input-validation boundary. No repository files were modified; this is the implementation plan.