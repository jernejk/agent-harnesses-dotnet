## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes the discount table using `customer.Tier`, but the table has no `standard` entry. Order 1001 belongs to a standard customer, so the lookup throws `KeyNotFoundException` and the total endpoint returns 500.

Commit `9bfbf7d` introduced the lookup. Previously, every non-gold tier received zero discount. The supplied problem-details diagnosis is inconsistent with the code: `AddProblemDetails()` is registered at `Program.cs:5`, and the unknown-order 404 test passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

```diff
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

Add a regression test in `tests/Orders.Tests/PricingTests.cs`, immediately after the standard-tier test:

```diff
     public void Standard_customers_pay_list_price_plus_gst() =>
         Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("standard")));

+    [Fact]
+    public void Unlisted_tiers_pay_list_price_plus_gst() =>
+        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("unlisted")));
+
     [Fact]
     public void Gold_customers_get_five_percent_off() =>
```

## Why this is the smallest safe fix

The lookup defaults unlisted tiers to `0m`, restoring the previous non-gold fallback while retaining the new silver discount. Existing tests cover standard, gold, silver, rounding, and endpoint status; the added test protects the fallback for other unlisted tiers. No 404-path change is needed.

## Verification (what to run and what it must show)

- Run `dotnet test`: all **9 tests** must pass. Before the fix, I reproduced **3 failures and 5 passes**, including the standard-tier exceptions and order 1001’s 500.
- Run the API and request `GET /orders/1001/total`: expect HTTP 200, total **195.80**, currency **AUD**.
- Check orders 1002 and 1003 still return totals **364.71** and **64.36**, respectively; an unknown order must still return 404.

## Risks

- Unrecognized tiers receive no discount rather than triggering an error. This preserves pre-1.4 behavior, but could conceal incorrect tier data.
- The change does not address null tiers or missing customer records; those are separate data-integrity risks.
- I could not check `https://status.example.com`: the available command policy blocked web access. No fix was applied.