## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts`, which contains `gold` and `silver` but not `standard` (lines 7–11). The lookup throws for order 1001’s standard-tier customer. Commit `9bfbf7d` replaced the previous zero-discount fallback with this lookup.

## Change (the exact lines before and after)

**Production:** in `src/Orders.Api/Pricing.cs:16`, replace:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var rate) ? rate : 0m;
```

**Pricing regression test:** in `tests/Orders.Tests/PricingTests.cs`, after the standard-tier test at line 14, add:

```csharp

    [Theory]
    [InlineData("unknown")]
    [InlineData(null)]
    public void Other_tiers_pay_list_price_plus_gst(string? tier) =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", tier!)));
```

**API amount regression test:** in `tests/Orders.Tests/OrdersApiTests.cs`, replace:

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
```

with:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Orders.Api;
```

Then, after the existing `Order_totals_are_available` test at line 17, add:

```csharp

    [Fact]
    public async Task Standard_order_total_has_the_correct_amount()
    {
        var response = await factory.CreateClient().GetAsync("/orders/1001/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new OrderTotal(1001, 195.80m, "AUD"),
            await response.Content.ReadFromJsonAsync<OrderTotal>());
    }
```

## Why this is the smallest safe fix

The single production-line change keeps the gold and silver rates and restores the former zero-discount fallback for missing tiers. Adding only a `standard` dictionary entry would fix this incident but leave other previously accepted tiers throwing. The tests cover that fallback and verify the API’s amount, not just its status.

## Verification (what to run and what it must show)

Run `dotnet test` after implementing the changes. All **11 tests** should pass, including standard pricing and rounding, gold and silver pricing, unknown and null tier fallback, and the HTTP 200 response with **195.80 AUD** for order 1001.

The current unmodified repository reproduces the reported baseline: **3 failed, 5 passed**. Do not use the localhost API to verify the patch: it remains on the deployed 1.4.0 build until redeployment.

## Risks

- Unknown or null tiers continue to receive no discount, matching the prior fallback but potentially masking bad customer data.
- A passing repository test does not fix the deployed service; deployment is out of scope.