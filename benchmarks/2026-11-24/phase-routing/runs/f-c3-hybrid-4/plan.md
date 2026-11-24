## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes a discount dictionary that contains only `gold` and `silver` (`:7–11`). The seeded customer for order 1001 is `standard` (`OrderStore.cs:8,14`), so the lookup throws.

Commit `9bfbf7d` replaced the 1.3.0 zero-discount fallback for non-gold tiers with the direct lookup. I reproduced the reported baseline: **3 failed, 5 passed**.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

```diff
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

Add a regression test after `tests/Orders.Tests/PricingTests.cs:14`:

```diff
     public void Standard_customers_pay_list_price_plus_gst() =>
         Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));
 
+    [Theory]
+    [InlineData("unrecognized")]
+    [InlineData(null)]
+    public void Other_tiers_pay_list_price_plus_gst(string? tier) =>
+        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier!)));
+
     [Fact]
```

## Why this is the smallest safe fix

The one-line production change retains gold and silver discounts while restoring the previous zero-discount fallback, including for a null tier. Adding only a `standard` entry would leave other previously accepted tiers throwing. Existing tests already cover standard pricing, rounding, and the affected route; the new test protects the broader fallback.

## Verification

After implementing the changes, run `dotnet test` from the repository root. All **10 test cases** should pass, including the standard pricing and rounding tests, both new fallback cases, and the API status tests for orders 1001–1003.

The existing API test asserts HTTP 200, not the response amount; the pricing tests assert the discount calculations. Do not use the localhost API to verify the patch: it remains on the deployed 1.4.0 build until redeployment.

## Risks

- Unrecognized or null tiers receive no discount rather than an error, matching the previous behavior but potentially concealing bad tier data.
- This plan has not been applied or tested against the patched code; redeployment is out of scope.