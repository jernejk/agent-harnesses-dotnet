## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts`, which contains gold and silver but not standard. Order 1001’s customer has tier `standard` (`OrderStore.cs:8,14`), so pricing throws `KeyNotFoundException` and the endpoint returns 500.

Commit `9bfbf7d8` replaced the prior zero-discount fallback with the lookup. The current `dotnet test` run reproduces **3 failures and 5 passes**.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`, replace:

```csharp
var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

Add an unknown-tier regression test after `PricingTests.cs:14`:

```csharp
    [Fact]
    public void Unknown_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("unknown")));
```

In `tests/Orders.Tests/OrdersApiTests.cs`, add these imports after `using System.Net;`:

```csharp
using System.Net.Http.Json;
using Orders.Api;
```

Replace lines 9–17:

```csharp
    [Theory]
    [InlineData(1001)]
    [InlineData(1002)]
    [InlineData(1003)]
    public async Task Order_totals_are_available(int orderId)
    {
        var response = await factory.CreateClient().GetAsync($"/orders/{orderId}/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
```

with:

```csharp
    [Theory]
    [InlineData(1001, 19580)]
    [InlineData(1002, 36471)]
    [InlineData(1003, 6436)]
    public async Task Order_totals_are_available(int orderId, int expectedCents)
    {
        var response = await factory.CreateClient().GetAsync($"/orders/{orderId}/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var total = await response.Content.ReadFromJsonAsync<OrderTotal>();
        Assert.NotNull(total);
        Assert.Equal(orderId, total.OrderId);
        Assert.Equal(expectedCents / 100m, total.Total);
        Assert.Equal("AUD", total.Currency);
    }
```

## Why this is the smallest safe fix

The one-line production change restores the previous zero-discount behavior for non-null unrecognized tiers while retaining the new silver discount. Existing tests cover standard pricing, rounding, gold and silver; the proposed additions protect the fallback and verify actual API totals, not just status codes.

## Verification (what to run and what it must show)

After implementing the changes, run `dotnet test` from the repository root. It must pass all **9 tests**, including standard pricing and rounding, the unknown-tier fallback, and the three API totals: 195.80, 364.71 and 64.36 AUD.

Do not use the localhost API to verify the patch: it runs the deployed 1.4.0 build until redeployment.

## Risks

- Unknown tiers will receive no discount, matching the earlier fallback; a future requirement may instead demand validation.
- `Customer.Tier` is declared non-null. A runtime null still throws in the dictionary lookup; handling invalid null data is outside this incident’s proposed fix.
- The fix will not affect the deployed service until a separate redeploy. No files were changed in this investigation.