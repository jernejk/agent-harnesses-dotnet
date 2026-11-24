## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes a discount table that contains gold and silver but not standard. This throws `KeyNotFoundException` for order 1001’s standard-tier customer.

Commit `9bfbf7d` replaced the previous zero-discount fallback for non-gold tiers with that direct lookup. I reproduced the reported baseline: **3 failed, 5 passed** with `dotnet test`.

## Change (the exact lines before and after)

**1. Restore the fallback** in `src/Orders.Api/Pricing.cs:16`.

Before:
```csharp
        var discount = TierDiscounts[customer.Tier];
```

After:
```csharp
        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var rate) ? rate : 0m;
```

**2. Add fallback regression coverage** in `tests/Orders.Tests/PricingTests.cs`, after line 14.

Before:
```csharp
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
```

After:
```csharp
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Theory]
    [InlineData("unknown")]
    [InlineData(null)]
    public void Unrecognized_tiers_pay_list_price_plus_gst(string? tier) =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier!)));

    [Fact]
```

**3. Check the incident response body** in `tests/Orders.Tests/OrdersApiTests.cs`.

Before, lines 1–2:
```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
```

After:
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Orders.Api;
```

Before, lines 15–16:
```csharp
        var response = await factory.CreateClient().GetAsync($"/orders/{orderId}/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```

After:
```csharp
        var response = await factory.CreateClient().GetAsync($"/orders/{orderId}/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (orderId == 1001)
            Assert.Equal(new OrderTotal(1001, 195.80m, "AUD"), await response.Content.ReadFromJsonAsync<OrderTotal>());
```

## Why this is the smallest safe fix

The production change is one line. It retains the configured gold and silver discounts and restores the old zero-discount behavior for all other tiers, rather than fixing only the `standard` key. The tests cover that fallback and the actual order-1001 total; existing tests cover standard pricing, rounding, gold and silver.

## Verification (what to run and what it must show)

After applying the changes, run **`dotnet test`** from the repository root. It must pass all **10** test cases, including the two new fallback cases, and show that order 1001 returns HTTP 200 with total **195.80 AUD**.

The patch has not been applied or tested. Do not use the localhost API to verify it: that service remains on the deployed 1.4.0 build until redeployment.

## Risks

The fallback can mask misspelled or newly introduced tier names by charging list price. That matches the pre-1.4 behavior; changing tier validation or alerting would be a separate decision.