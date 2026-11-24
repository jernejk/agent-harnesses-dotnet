The reviewer’s critique is valid: the existing tests do not cover the null fallback. Although `Customer.Tier` is declared as non-nullable, a runtime null is possible; the previous comparison treated it as a non-gold tier and applied no discount. The revised plan adds a regression test for that behavior.

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table contains `gold` and `silver`, but not `standard`, so the lookup for order 1001 throws `KeyNotFoundException` and the API returns HTTP 500.

The previous pricing logic, confirmed in the parent of commit `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0`, applied a discount only when `Tier == "gold"`; other values, including null, got no discount.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, add this test after the standard-customer test:

```csharp
    [Fact]
    public void Null_tier_uses_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", null!)));
```

The `null!` deliberately exercises a runtime-null value while suppressing the compiler warning for the model’s non-nullable `string` declaration.

## Why this is the smallest safe fix

The production change is limited to the failing lookup line. It keeps the current `gold` and `silver` discounts and gives zero discount when the tier is unmapped or null, matching the prior fallback behavior. The added test covers the null path explicitly. Subtotal calculation, GST, and rounding remain unchanged.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

After implementing both changes, the suite should report **9 passed, 0 failed**. That should include the null-tier pricing test, standard/gold/silver pricing, rounding, successful totals for orders 1001–1003, and the unknown-order 404.

The baseline `dotnet test` run before the fix reported **3 failed, 5 passed**. No live API call is appropriate: localhost is still running the deployed 1.4.0 build, and redeployment is out of scope.

## Risks

Unmapped or null tiers continue to receive zero discount rather than causing an exception, consistent with the prior behavior. The regression test uses a null-forgiving expression because null violates the model’s compile-time annotation. No code has been changed, and the deployed localhost service will remain on 1.4.0 until redeployed.