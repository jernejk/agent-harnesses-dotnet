Both critique points are valid, with one qualification: the existing pricing tests already check the gold and silver discounts, so those assertions should be retained rather than duplicated. The null check must happen before the lookup; `GetValueOrDefault` alone is not safe for a null dictionary key.

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 contains only `"gold"` and `"silver"`. Order `1001` belongs to the `"standard"` customer seeded in `src/Orders.Api/OrderStore.cs:8,14`, so the lookup throws `KeyNotFoundException`. The exception propagates through the totals endpoint at `src/Orders.Api/Program.cs:23–24` and results in HTTP 500.

Commit `9bfbf7d` introduced the lookup. The prior implementation gave zero discount to tiers other than `"gold"`—including a null tier—whereas the lookup throws for an unlisted tier. Although `Customer.Tier` is declared non-nullable in `src/Orders.Api/Models.cs:3`, a null can still reach the pricing method at runtime.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16.

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

In `tests/Orders.Tests/PricingTests.cs`, keep the existing standard, gold, and silver tests, and add cases for null and an unlisted tier.

**Before** (relevant existing test section)
```csharp
    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
        Assert.Equal(104.50m, Pricing.Total(HundredDollars, Tier("gold")));

    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));
```

**After**
```csharp
    [Fact]
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Null_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", null!)));

    [Fact]
    public void Unlisted_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("platinum")));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
        Assert.Equal(104.50m, Pricing.Total(HundredDollars, Tier("gold")));

    [Fact]
    public void Silver_customers_get_two_percent_off() =>
        Assert.Equal(107.80m, Pricing.Total(HundredDollars, Tier("silver")));
```

## Why this is the smallest safe fix

The production change is one line. It explicitly avoids looking up a null key and uses a zero fallback for other unlisted tiers, restoring the prior zero-discount behavior while preserving the configured gold and silver discounts. The two new tests cover the null and unlisted cases; the existing tests already verify standard, gold, and silver pricing. No endpoint change is needed.

## Verification (what to run and what it must show)

From the repo root, run:

```sh
dotnet test
```

Before the fix, this run produced 3 failures and 5 passes, including the standard-tier pricing failures and the API totals case for order `1001`. After the changes, it must pass all 10 cases: the existing 8 plus the two new pricing tests. The API test uses `WebApplicationFactory<Program>`, so it exercises the newly built code rather than the deployed localhost service. Do not use localhost for verification: it remains on deployed version 1.4.0, and redeployment is out of scope.

## Risks

Null and unlisted tiers will receive zero discount, matching the previous behavior; if a new tier needs a specific discount, it should be added explicitly. The lookup remains case-sensitive. No source files were changed during this review.