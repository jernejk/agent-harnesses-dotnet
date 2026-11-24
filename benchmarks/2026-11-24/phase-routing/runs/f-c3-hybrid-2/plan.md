## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table at lines 7–11 contains `gold` and `silver`, but not `standard`. Order 1001 belongs to a standard customer (`OrderStore.cs:8,14`), so pricing throws `KeyNotFoundException`; the total route does not catch it.

Commit `9bfbf7d8` introduced the lookup. In 1.3.0, non-gold tiers received zero discount. The baseline `dotnet test` run confirmed the reported result: **3 failed, 5 passed**.

## Change (the exact lines before and after)

**1. Replace `src/Orders.Api/Pricing.cs:16`.**

Before:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

After:

```csharp
        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var rate) ? rate : 0m;
```

**2. Add fallback regression tests after `tests/Orders.Tests/PricingTests.cs:14`.**

Before:

```csharp
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));
```

After:

```csharp
    public void Standard_customers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

    [Fact]
    public void Unlisted_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("unlisted")));

    [Fact]
    public void Null_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(null!)));
```

**3. Add an API response-value test in `tests/Orders.Tests/OrdersApiTests.cs`.**

Replace the imports:

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

Then replace the boundary before the existing 404 test:

```csharp
    [Fact]
    public async Task Unknown_orders_return_404()
```

with:

```csharp
    [Fact]
    public async Task Standard_customer_order_returns_expected_total()
    {
        var response = await factory.CreateClient().GetAsync("/orders/1001/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new OrderTotal(1001, 195.80m, "AUD"),
            await response.Content.ReadFromJsonAsync<OrderTotal>());
    }

    [Fact]
    public async Task Unknown_orders_return_404()
```

## Why this is the smallest safe fix

The production change is one line. It restores the previous zero-discount fallback for unlisted and null tiers while retaining the 1.4.0 gold and silver rates. The added tests cover fallback compatibility and ensure that a successful response for order 1001 contains the correct total, not just a 200 status.

## Verification (what to run and what it must show)

After implementing the changes, run `dotnet test` from the repo root. All **11 tests** must pass, including:

- Standard, unlisted, and null tiers receive no discount.
- Gold and silver retain their existing discounts; rounding still passes.
- Order 1001 returns 200 with `195.80 AUD`; orders 1002 and 1003 return 200; an unknown order returns 404.

Do not use the live API to verify the patch: localhost runs the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

The fallback silently treats misspelled or newly introduced tiers as undiscounted, matching the earlier behavior but potentially concealing data errors. No patch has been applied or verified; the current baseline still fails.