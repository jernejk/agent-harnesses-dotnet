## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 has no `standard` entry. Order 1001 belongs to a standard customer (`OrderStore.cs:8,14`), so the lookup throws and the total endpoint returns HTTP 500.

Commit `9bfbf7d` introduced the lookup. In 1.3.0, every non-gold tier received a zero discount. I confirmed the reported baseline with `dotnet test`: three failures and five passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

```diff
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var tierDiscount) ? tierDiscount : 0m;
```

Add regression tests after `tests/Orders.Tests/PricingTests.cs:14`:

```diff
+    [Theory]
+    [InlineData("bronze")]
+    [InlineData("Gold")]
+    [InlineData(null)]
+    public void Other_tiers_pay_list_price_plus_gst(string? tier) =>
+        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier!)));
+
```

In `tests/Orders.Tests/OrdersApiTests.cs`, add the imports:

```diff
 using System.Net;
+using System.Net.Http.Json;
 using Microsoft.AspNetCore.Mvc.Testing;
+using Orders.Api;
```

Add this test after the existing order-total theory:

```diff
+    [Fact]
+    public async Task Standard_order_total_includes_gst()
+    {
+        var response = await factory.CreateClient().GetAsync("/orders/1001/total");
+        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
+        Assert.Equal(new OrderTotal(1001, 195.80m, "AUD"),
+            await response.Content.ReadFromJsonAsync<OrderTotal>());
+    }
+
```

## Why this is the smallest safe fix

The production change is one line. It restores the old zero-discount fallback for unrecognized or null tiers while preserving the new silver discount and existing gold discount. Adding only `standard` to the table would fix this order but leave the other fallback regressions.

The new tests protect the fallback and the incident’s actual response amount; existing tests already cover standard, gold, silver, rounding, and order availability.

## Verification (what to run and what it must show)

After implementing the changes, run:

```text
dotnet test Orders.slnx
```

All tests must pass, including the three currently failing tests and the new regressions. The order 1001 test must verify HTTP 200 and `195.80 AUD`; gold and silver tests must retain their existing expected totals.

Do not verify the patch against the localhost API: it remains the deployed 1.4.0 build until a redeploy.

## Risks

- Defaulting unknown tiers to zero can conceal misspelled tier values, but matches 1.3.0 behavior. A stricter policy would be a separate behavior change.
- The production incident persists until the fix is deployed; redeployment is outside this plan.