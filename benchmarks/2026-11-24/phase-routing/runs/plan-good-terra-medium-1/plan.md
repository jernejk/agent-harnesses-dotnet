## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes a discount table that contains `gold` and `silver`, but not `standard`. The lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer (`OrderStore.cs:8,14`).

Commit `9bfbf7d` replaced the previous zero-discount fallback for non-gold tiers with the direct lookup. I reproduced the reported baseline: **3 failed, 5 passed**.

I could not check `https://status.example.com`: external HTTP access is unavailable through the permitted tools.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

```diff
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

Add an unknown-tier regression test after the standard-tier test in `tests/Orders.Tests/PricingTests.cs`:

```diff
     public void Standard_customers_pay_list_price_plus_gst() =>
         Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

+    [Fact]
+    public void Unknown_tiers_pay_list_price_plus_gst() =>
+        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("unknown")));
+
     [Fact]
```

Extend the existing API test in `tests/Orders.Tests/OrdersApiTests.cs` to verify order 1001’s response body:

```diff
 using System.Net;
+using System.Net.Http.Json;
 using Microsoft.AspNetCore.Mvc.Testing;
+using Orders.Api;
```

```diff
         var response = await factory.CreateClient().GetAsync($"/orders/{orderId}/total");
         Assert.Equal(HttpStatusCode.OK, response.StatusCode);
+        if (orderId == 1001)
+        {
+            var total = await response.Content.ReadFromJsonAsync<OrderTotal>();
+            Assert.NotNull(total);
+            Assert.Equal(1001, total.OrderId);
+            Assert.Equal(195.80m, total.Total);
+            Assert.Equal("AUD", total.Currency);
+        }
```

## Why this is the smallest safe fix

The production change is one line. It restores the former zero-discount behavior for missing tiers while preserving the configured gold and silver discounts, GST calculation, and rounding. The added tests cover the previously implicit fallback and the affected API result.

## Verification

After applying the changes, run `dotnet test`. It should report **9 passed, 0 failed**: the existing standard, rounding, gold, silver, API, and 404 tests must pass alongside the new unknown-tier test. Order 1001’s API test must confirm HTTP 200 and a total of **195.80 AUD**.

For a deployed smoke test, request `GET /orders/1001/total` and confirm the same status and total; also confirm `/health` returns 200.

## Risks

- Unknown non-null tiers will receive no discount, matching the pre-1.4 behavior but potentially masking misspelled tier values.
- A null tier remains outside this fix; the dictionary lookup rejects null keys.
- The proposed changes have **not been applied or tested**.