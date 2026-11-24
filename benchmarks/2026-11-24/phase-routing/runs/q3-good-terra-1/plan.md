## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly. The table contains `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer (`OrderStore.cs:8,14`).

Commit `9bfbf7d` replaced the previous zero-discount fallback for non-gold tiers with the direct lookup. I reproduced the reported baseline: **3 failed, 5 passed**.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

```diff
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
+            ? tierDiscount : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, add the following after the standard-customer test:

```diff
     public void Standard_customers_pay_list_price_plus_gst() =>
         Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

+    [Theory]
+    [InlineData("unrecognized")]
+    [InlineData(null)]
+    public void Other_tiers_pay_list_price_plus_gst(string? tier) =>
+        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", tier!)));
+
     [Fact]
     public void Gold_customers_get_five_percent_off() =>
```

## Why this is the smallest safe fix

The guarded lookup preserves the gold and silver discounts while restoring the former zero-discount fallback for standard, unknown, and runtime-null tiers. The new cases protect that fallback; existing tests cover standard pricing, rounding, gold, silver, and API status.

## Verification (what to run and what it must show)

After applying the changes, run `dotnet test`. It should report **10 passed, 0 failed**, including:

- Standard and other fallback tiers pay list price plus GST.
- Gold and silver retain their discounts; rounding remains correct.
- The in-process API test for order 1001 returns HTTP 200, and the other API tests pass.

The current API test checks the status, not the response total. Do not use the localhost API to verify the patch: it remains on the deployed 1.4.0 build until a redeploy.

## Risks

Unknown or null tiers receive no discount rather than surfacing a data error. That restores the pre-1.4 behavior but could conceal invalid tier data. The deployed incident will persist until the patch is redeployed, which is out of scope.