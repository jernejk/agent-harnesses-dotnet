## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` directly with `customer.Tier`. The dictionary at lines 7–11 contains only `"gold"` and `"silver"`, so the `"standard"` tier throws `KeyNotFoundException`.

`src/Orders.Api/OrderStore.cs:8,14` maps order 1001 to a standard-tier customer. The `9bfbf7d` change replaced the prior behavior—gold received a discount and every other tier received zero—with the direct lookup. The current API route passes the stored customer to `Pricing.Total` at `src/Orders.Api/Program.cs:24`.

The reported baseline is confirmed: `dotnet test` produced 3 failures and 5 passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
```

`GetValueOrDefault` preserves configured gold and silver discounts and supplies zero for an unlisted tier. The null check prevents passing a null key to the dictionary.

Add these tests to `tests/Orders.Tests/PricingTests.cs`, after the existing standard-tier test:

```csharp
    [Fact]
    public void Other_unlisted_tiers_pay_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier("bronze")));

    [Fact]
    public void Null_tier_pays_list_price_plus_gst() =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, new Customer(1, "Test", null!)));
```

The `null!` test deliberately exercises a runtime null despite `Customer.Tier` being declared non-nullable.

## Why this is the smallest safe fix

The production change is one line and keeps the existing lookup table and pricing formula intact. It restores the former zero-discount behavior for tiers not in the table while avoiding a null-key exception. The added tests cover another unlisted tier and the defensive null case.

The reviewer is right that the original plan’s `GetValueOrDefault` line alone is not null-safe and that a second unlisted tier was untested. However, the assertion that the existing tests only establish standard-tier behavior does not match the repository: `PricingTests.cs` already tests the exact gold and silver discounts, and the API test exercises orders 1001, 1002, and 1003. Those existing tests should remain unchanged.

## Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

It must pass all tests. The suite currently has 8 cases; adding the two pricing regressions makes 10. The passing results must include the standard-tier pricing and rounding cases, the new bronze and null cases, the existing gold and silver discount tests, and the API request for order 1001.

Do not verify against the live API: it is still the deployed 1.4.0 build, and redeployment is out of scope. No files have been changed.

## Risks

Treating any unlisted tier as zero-discount also means a misspelled or newly introduced tier will not fail fast. That matches the previous fallback behavior. Null is outside the declared non-nullable `Customer.Tier` contract; the guard and test intentionally make pricing defensive if such a value is supplied at runtime.