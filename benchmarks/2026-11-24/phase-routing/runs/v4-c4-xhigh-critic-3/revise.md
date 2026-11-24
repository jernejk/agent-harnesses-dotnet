## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table at lines 7–11 contains `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer, and the exception produces the HTTP 500.

The regression commit’s diff confirms that before the lookup table, only gold received a discount; every other tier received zero. The silver discount is intentional and should remain.

## Change (the exact lines before and after)

**In `src/Orders.Api/Pricing.cs`, replace line 16.**

Before:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

After:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

**In `tests/Orders.Tests/PricingTests.cs`, add tests for unlisted and null tiers before the existing rounding test.**

Before:

```csharp
    [Fact]
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
```

After:

```csharp
    [Fact]
    public void Unlisted_tiers_get_no_discount() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("platinum")));

    [Fact]
    public void Null_tiers_get_no_discount() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", null!)));

    [Fact]
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
```

The `null!` test deliberately exercises the prior runtime behavior; `Customer.Tier` is declared non-nullable, but the pre-regression comparison treated a null tier as a non-gold tier and applied no discount.

**In `tests/Orders.Tests/OrdersApiTests.cs`, deserialize the response and assert its total.** Add these imports:

```csharp
using System.Globalization;
using System.Net.Http.Json;
using Orders.Api;
```

Change the theory data and method from:

```csharp
    [InlineData(1001)]
    [InlineData(1002)]
    [InlineData(1003)]
    public async Task Order_totals_are_available(int orderId)
    {
        var response = await factory.CreateClient().GetAsync($"/orders/{orderId}/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
```

to:

```csharp
    [InlineData(1001, "195.80")]
    [InlineData(1002, "364.71")]
    [InlineData(1003, "64.36")]
    public async Task Order_totals_are_available(int orderId, string expectedTotal)
    {
        var response = await factory.CreateClient().GetAsync($"/orders/{orderId}/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderTotal = await response.Content.ReadFromJsonAsync<OrderTotal>()
            ?? throw new InvalidOperationException("Expected an order total response.");
        Assert.Equal(decimal.Parse(expectedTotal, CultureInfo.InvariantCulture), orderTotal.Total);
    }
```

## Why this is the smallest safe fix

The pricing change is limited to the failing lookup. Gold and silver still use their existing table rates; standard, unlisted, and null tiers receive no discount, preserving the previous behavior. The added pricing tests lock in that fallback, and the API test change verifies the calculated response rather than status alone.

The third critique’s redeploy concern does not apply here: `OrdersApiTests` uses `WebApplicationFactory<Program>` and `factory.CreateClient()`, which exercises an in-process test host, not the deployed localhost service.

## Verification (what to run and what it must show)

Run from the repository root:

```text
dotnet test
```

After implementation, it should rebuild and pass all 10 test cases (0 failed):

- Standard pricing is `110.00`; standard rounding remains `21.89`.
- Gold pricing remains `104.50`; silver remains `107.80`.
- Unlisted and null tiers return `110.00` for the test order.
- API totals are `195.80` for order 1001, `364.71` for 1002, and `64.36` for 1003; all return HTTP 200.
- An unknown order still returns HTTP 404.

The current, pre-fix `dotnet test` run reproduced 3 failures and 5 passes, including the `KeyNotFoundException` at `Pricing.cs:16` and the API test failure for order 1001. Do not use the live localhost API for verification: it remains on deployed build 1.4.0, and redeployment is out of scope.

## Risks

The `null!` case intentionally tests behavior outside the model’s non-nullable `Tier` declaration. The fallback also treats any unrecognized tier as zero-discount, matching the old behavior; if tier validation is later required, it should be handled as a separate change. The API test will assert the returned amount but not the currency field.