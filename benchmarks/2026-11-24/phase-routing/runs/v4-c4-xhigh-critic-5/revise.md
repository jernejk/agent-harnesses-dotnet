## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 contains `"gold"` and `"silver"`, but not `"standard"`, so `Pricing.Total` throws `KeyNotFoundException` for a standard-tier customer.

Order 1001 uses customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`). The total endpoint calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`. The parent version used a fallback that gave zero discount to every tier except exact `"gold"`; the new lookup removed that fallback while adding silver pricing.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

Add regression tests to `tests/Orders.Tests/PricingTests.cs`. Insert these after the standard-tier test and before the gold-tier test:

**Before** (surrounding lines)
```csharp
    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
```

**After**
```csharp
    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Null_tier_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", null!)));

    [Fact]
    public void Unmapped_tier_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("platinum")));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
```

The null test deliberately uses `null!` to exercise the defensive behavior even though `Customer.Tier` is declared non-nullable.

## Why this is the smallest safe fix

The production change is limited to one line in one file. Known gold and silver tiers continue to use their existing table discounts; null and unmapped tiers receive zero discount, restoring the previous fallback behavior. The two added tests cover those fallback cases directly without changing the customer model or API contract.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

Before the fix, this command reproduced **3 failed, 5 passed**: the two standard-tier pricing tests and the API total test for order 1001 failed. After the fix and test additions, it should show **10 passed, 0 failed**: the original eight tests plus the null-tier and unmapped-tier tests. The API cases should pass for seeded orders 1001–1003, and the unknown-order case should still return 404.

The API tests use `WebApplicationFactory<Program>` and the test project references `Orders.Api`, so they exercise the repo’s test build in-process—not the deployed localhost service. No live API call or redeploy is needed for this verification.

## Risks

Unmapped tiers will continue to receive zero discount, matching the old fallback; any new tier that should receive a discount needs an explicit table entry. `Customer.Tier` is non-nullable, so the null case is defensive coverage for a value that is outside its declared contract. The deployed 1.4.0 service remains unchanged until redeploy, which is outside this plan. No source files were changed during this review.