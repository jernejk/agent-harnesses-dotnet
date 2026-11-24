## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 has no `standard` entry. The cited commit replaced the previous zero-discount fallback with that direct lookup.

Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`). I reproduced the reported result: **3 failed, 5 passed**, including two `KeyNotFoundException`s and the API test’s HTTP 500.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

```diff
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var rate) ? rate : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, insert after the standard-tier test at line 14:

```diff
     public void Standard_customers_pay_list_price_plus_gst() =>
         Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));
 
+    [Theory]
+    [InlineData("bronze")]
+    [InlineData("Gold")]
+    public void Unrecognized_tiers_pay_list_price_plus_gst(string tier) =>
+        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier)));
+
     [Fact]
     public void Gold_customers_get_five_percent_off() =>
```

## Why this is the smallest safe fix

The one-line production change restores the prior fallback for unrecognized non-null tiers, rather than fixing only `standard`. It leaves the gold and newly added silver discounts, GST, and rounding unchanged. The new test covers an unknown tier and case-sensitive matching; existing tests cover standard pricing and the API failure.

## Verification (what to run and what it must show)

After implementing the changes, run `dotnet test` from the repository root. It must report **10 passed, 0 failed**: the original eight cases plus the two new theory cases. In particular, order 1001’s API test must return HTTP 200, and the pricing tests must confirm standard, gold, silver, rounding, and fallback behavior.

The existing API test checks status, not the response body. Do not use the live API to verify the patch: localhost remains on the deployed 1.4.0 build until a redeploy.

## Risks

- Unknown tiers will receive no discount, matching the prior fallback; a mistyped tier will not be reported as an error.
- `Customer.Tier` is declared non-nullable. A runtime null would still make the dictionary lookup throw; handling invalid null data is outside this minimal fix.
- This plan has not been applied or tested as a patch. The deployed service remains affected until redeployment.