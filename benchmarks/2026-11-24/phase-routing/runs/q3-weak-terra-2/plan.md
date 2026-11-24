## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes a discount dictionary that contains `gold` and `silver`, but not `standard`. Commit `9bfbf7d` introduced that lookup; previously, non-gold tiers received a zero discount.

Order 1001 belongs to a standard customer (`OrderStore.cs:8,14`). The pricing tests reproduce a `KeyNotFoundException` for that tier, explaining the endpoint failure.

The reported problem-details cause is not supported: `Program.cs:5` already calls `AddProblemDetails()`, and the unknown-order test passes. The problem-details branch is not used for order 1001.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = TierDiscounts.TryGetValue(customer.Tier, out var rate) ? rate : 0m;
```

Add a regression test in `tests/Orders.Tests/PricingTests.cs`, immediately after the standard-tier test:

**Before**
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
    public void Unrecognized_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("unrecognized")));

    [Fact]
    public void Gold_customers_get_five_percent_off() =>
```

## Why this is the smallest safe fix

The one-line production change restores the previous zero-discount fallback while retaining the new silver discount. The added test protects that fallback; existing tests cover standard, gold, silver, rounding, and endpoint status. No endpoint or problem-details changes are needed.

## Verification (what to run and what it must show)

After implementing the changes, run `dotnet test` from the repo root. All **9 tests** should pass, including order 1001 returning HTTP 200, the standard-tier pricing and rounding tests, the new fallback test, and the unknown-order 404 test.

The current unmodified tree reproduces **3 failed, 5 passed**. Do not verify the patch against localhost: it runs the deployed 1.4.0 build until a redeploy.

## Risks

- Unrecognized non-null tier names, including typos, receive no discount rather than raising an error. This matches pre-1.4.0 behavior.
- The existing API test checks status, not the response amount. The pricing tests independently check the calculation.
- No changes were applied or tested as a patch; redeployment is out of scope.