## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes a dictionary containing only `gold` and `silver`. Order 1001’s customer has tier `standard`, so the lookup throws and the endpoint returns HTTP 500. Commit `9bfbf7d` replaced the previous zero-discount fallback with this lookup.

I reproduced the reported test result: **3 failed, 5 passed**.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

**Before**
```csharp
        var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var rate)
            ? rate
            : 0m;
```

Add regression coverage after `tests/Orders.Tests/PricingTests.cs:14`:

**Before**
```csharp
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
```

**After**
```csharp
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Theory]
    [InlineData("bronze")]
    [InlineData(null)]
    public void Unrecognized_tiers_receive_no_discount(string? tier) =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier!)));

    [Fact]
```

## Why this is the smallest safe fix

The existing standard-tier tests cover the incident. The lookup fallback restores the previous behavior for other unrecognized tiers and null, while retaining the new silver discount. Adding only a `standard` entry would leave other formerly accepted tiers broken.

## Verification

After implementing the change:

- Run `dotnet test`: all **10 test cases** should pass, including standard, unknown and null tiers, gold, silver, and the API tests.
- Run the service and check the total endpoints: orders 1001, 1002 and 1003 should return HTTP 200 with totals **195.80**, **364.71**, and **64.36** AUD respectively. An unknown order should still return 404.

## Risks

Unknown or null tiers silently receive no discount, preserving the old behavior but potentially masking bad tier data. A missing customer is a separate issue and is not addressed by this change.

No fix was applied.