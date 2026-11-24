### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts`, but the dictionary at lines 7–11 contains only `gold` and `silver`. `src/Orders.Api/OrderStore.cs:8` assigns order 1001’s customer the `standard` tier, and `src/Orders.Api/Program.cs:24` passes that customer to `Pricing.Total`. The missing key causes the `KeyNotFoundException` and the HTTP 500.

The cause is supported by the cited commit: it replaced the earlier “gold gets 5%; all other tiers get zero” conditional with the direct lookup. The current test run reproduces the report: 3 failed and 5 passed, including the standard-tier pricing tests and the order 1001 API test.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace this line:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
```

### Why this is the smallest safe fix

It changes only the failing lookup. Known `gold` and `silver` tiers keep their configured discounts; `standard` and other unlisted tiers receive zero discount, matching the previous implementation’s behavior for non-gold tiers. It avoids changing the endpoint, customer data, or discount rates.

### Verification (what to run and what it must show)

Run:

```text
dotnet test
```

After the change, the full suite should pass: **8 passed, 0 failed**. In particular, the standard-tier pricing tests and the API test for order 1001 should pass, alongside the gold/silver pricing and other order-total tests.

The current deployed localhost service is still 1.4.0, so the repository patch will not verify against it without a redeploy. Use the test suite for verification.

### Risks

Unlisted or misspelled tier names will continue to receive zero discount, preserving the prior behavior; this can conceal invalid tier data. If unknown tiers should instead be rejected, that requires an explicit validation policy and is separate from this minimal regression fix. No files were modified.