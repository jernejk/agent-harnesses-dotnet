## Root cause (file:line)

- `src/Orders.Api/Pricing.cs:16` indexes a dictionary containing only `gold` and `silver` (lines 7–11). Looking up `standard` throws `KeyNotFoundException`.
- `OrderStore.cs:8,14` assigns `standard` to order 1001’s customer. The exception escapes the total route at `Program.cs:24`, producing HTTP 500.
- Commit `9bfbf7d` introduced the lookup. In 1.3.0, every non-gold tier received a zero discount. The regression therefore also affects other unrecognized tiers, not just `standard`.

## Change (the exact lines before and after)

**Pricing:** replace `src/Orders.Api/Pricing.cs:16`.

Before:
```csharp
        var discount = TierDiscounts[customer.Tier];
```

After:
```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

**Regression tests:** in `tests/Orders.Tests/PricingTests.cs`, insert the following immediately before the existing `[Fact]` at line 12:

```csharp
    [Theory]
    [InlineData("unknown")]
    [InlineData("STANDARD")]
    [InlineData(null)]
    public void Unrecognized_tiers_pay_list_price_plus_gst(string? tier) =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier!)));

```

In `tests/Orders.Tests/OrdersApiTests.cs`, add these imports after `using System.Net;`:

```csharp
using System.Net.Http.Json;
using Orders.Api;
```

Then insert this test immediately before the existing `[Fact]` at line 19:

```csharp
    [Fact]
    public async Task Standard_order_total_has_expected_amount_and_currency()
    {
        var response = await factory.CreateClient().GetAsync("/orders/1001/total");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var total = await response.Content.ReadFromJsonAsync<OrderTotal>();
        Assert.Equal(new OrderTotal(1001, 195.80m, "AUD"), total);
    }

```

## Why this is the smallest safe fix

The production change is one line. It restores the previous zero-discount fallback, including for null, while retaining the new silver discount and existing rounding. Adding only a `standard` dictionary entry would fix the reported order but leave other previously accepted tiers throwing.

## Verification (what to run and what it must show)

Run `dotnet test` from the repository root after making the changes. It should show **12 passed, 0 failed**: the existing standard, rounding, gold, silver, API and 404 tests; three fallback cases; and the new order 1001 amount and currency test.

The unmodified baseline was reproduced: **3 failed, 5 passed**. The live localhost service remains the deployed 1.4.0 build; the repo patch cannot verify or change it without a redeploy, which is out of scope.

## Risks

Unknown or misspelled tiers receive no discount silently, matching 1.3.0 behavior; this could mask bad tier data. A missing customer remains a separate unhandled condition and is not addressed by this incident fix. No files were changed.