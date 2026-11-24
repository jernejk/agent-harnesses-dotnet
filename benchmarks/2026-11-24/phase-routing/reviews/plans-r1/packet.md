# Blind review: plan

You are one of three independent reviewers. Each item below is a fix plan written by a planner for the engineer who implements it. Some planners received correct investigation findings, others received findings that blame the red herring; each item says which. A good plan checks the findings against the code and plans the right fix either way. The items come from
different AI setups; their names are hidden, and the order is random. Judge only what is on the
page. Do not guess which model wrote an item.

## Ground truth

- The incident: since the 1.4.0 deploy, GET /orders/{id}/total returns HTTP 500 (order 1001, for
  example); 1.3.0 was fine.
- The real cause: commit 9bfbf7d ("Move tier discounts into a lookup table and add silver tier")
  replaced the old rule (gold 5%, every other tier 0) with `TierDiscounts[customer.Tier]` at
  src/Orders.Api/Pricing.cs line 16. The table has gold and silver but no "standard", so every
  standard-tier customer throws KeyNotFoundException, which surfaces as HTTP 500. `dotnet test`
  shows 3 failures (two pricing tests and the API test for order 1001) and 5 passes.
- Good fixes: default a missing tier to a 0 discount (GetValueOrDefault, or TryGetValue with 0m).
  Adding a "standard" entry fixes the reported customers, but any other unknown tier still throws.
  After the fix, `dotnet test` shows 8 passed, 0 failed.
- The red herring: commit ab38e53 ("Return problem details for unknown orders") changed
  Program.cs's not-found helper to Results.Problem(..., 404). It is not the cause: order 1001
  exists, so that code path never runs for it.
- The running Orders service is a published copy of 1.4.0. A patch to the source does not
  change it until someone redeploys, so the live endpoint still returns 500 after a correct
  patch. That is expected, not a second bug.
- https://status.example.com cannot be reached with the tools given; saying so in one line is
  fine, dwelling on it is noise.

## How to score

For every item, score each criterion from 1 (poor) to 5 (excellent):
- correctness: plans a fix for the real cause; with misleading findings, notices and corrects them
- precision: exact file, line and before/after code
- safety: smallest safe change, no unrelated edits
- verification: concrete check with the right expected result
- clarity: easy to act on, no noise

Then give an overall score from 1 to 10, set "wrong" to true if the item would lead an engineer
to the wrong fix or states something false as fact, and add a one-sentence note. Finally rank all
items from best to worst. Return only JSON in the requested format.

## Item P01

Received: misleading findings (they blame ab38e53)

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

## Item P02

Received: misleading findings (they blame ab38e53)

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–16`: the tier-discount dictionary contains `gold` and `silver`, but not `standard`. Line 16 indexes it using the customer’s tier, so `standard` throws `KeyNotFoundException`.

`src/Orders.Api/OrderStore.cs:8,14` maps order `1001` to a `standard` customer. The regression was introduced in `9bfbf7d`, which replaced the previous “gold gets 5%, otherwise 0%” logic with the dictionary lookup. The test inventory matches the reported failures: two standard-tier pricing tests and the API test for order 1001.

The reported cause in `Program.cs:30` is not supported by the current code: `AddProblemDetails()` is registered at line 5, and the not-found helper is only used when the order is missing. Order 1001 proceeds to `Pricing.Total`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the existing `standard` tier.

**Before:**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After:**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

Leave the lookup on line 16 unchanged.

## Why this is the smallest safe fix

One dictionary entry restores the prior behavior for standard customers while preserving the intended gold and silver discounts. It avoids changing endpoint handling or making unrecognized tiers silently receive a zero discount.

## Verification (what to run and what it must show)

After applying the change, run `dotnet test`. It should report **8 passed, 0 failed**, including the standard-tier pricing tests and `Order_totals_are_available(orderId: 1001)`. The existing gold, silver, rounding, and unknown-order tests should continue to pass.

Also verify `GET /orders/1001/total` returns **200** with a total of **195.8 AUD**; `GET /orders/9999/total` should remain **404**.

## Risks

An unrecognized tier will still throw; this fix deliberately handles the known `standard` tier without defining a fallback policy for unknown values. I could not verify `https://status.example.com`: the available tools did not provide an HTTP fetch, and the attempted `curl` request was denied. No repository files were changed, and I did not run the tests.

## Item P03

Received: correct findings

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` using the customer’s tier. The table contains `gold` and `silver`, but not `standard`, so the lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`).

I reproduced the reported test result: **3 failed, 5 passed**.

## Change (the exact lines before and after)

At `src/Orders.Api/Pricing.cs:16`:

**Before**
```cs
var discount = TierDiscounts[customer.Tier];
```

**After**
```cs
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

It is a one-line change that keeps the gold and silver discounts while restoring the prior zero-discount fallback for tiers not in the table. That gives standard customers list price plus GST without changing the API route or order data.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It must show **8 passed, 0 failed**, including both standard-tier pricing tests and the API test for order 1001. The API test should receive HTTP 200; the order total should be **195.80 AUD**.

## Risks

- Unmapped or misspelled tiers will receive a zero discount instead of failing fast. This matches the pre-1.4.0 fallback behavior; confirm that behavior remains intended.
- I could not verify `https://status.example.com`: the available command tool blocked the network request, and no HTTP-fetch tool was available. No status-page incident state is inferred.
- No source changes were made.

## Item P04

Received: misleading findings (they blame ab38e53)

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:10,16`: the tier-discount lookup has entries for `"gold"` and `"silver"` but not `"standard"`, and `Pricing.Total` indexes it directly. Order 1001 belongs to a `"standard"` customer, so the lookup throws `KeyNotFoundException` and the endpoint returns HTTP 500.

The omission was introduced by `9bfbf7d` (“Move tier discounts into a lookup table and add silver tier”), which replaced the previous zero-discount fallback. The cited `ab38e53` change is not the cause: `AddProblemDetails()` is registered in `Program.cs:5`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for standard customers:

**Before**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It restores the existing standard-customer pricing behavior without changing endpoint error handling, discount calculations, or supported gold and silver discounts. Existing tests already cover standard-customer pricing and the order-total endpoint.

## Verification (what to run and what it must show)

Run `dotnet test`. It should report **8 passed, 0 failed**, including the standard pricing tests and `OrdersApiTests.Order_totals_are_available(orderId: 1001)`. That endpoint test should receive HTTP 200 rather than 500.

## Risks

This only handles the `"standard"` tier already present in the order data; an unrecognized tier would still cause a lookup exception. The requested status-page check could not be completed: the environment denied the HTTP request and instructed me not to retry it. No code was changed.

## Item P05

Received: correct findings

Status-page check: **I couldn’t verify `https://status.example.com`**. The available read-only command interface denied the HTTP request, and no alternate HTTP-fetch tool is available here. The diagnosis below is verified against the repository.

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` looks up every customer tier directly in `TierDiscounts`. That table contains `gold` and `silver`, but not `standard`. For order 1001, the standard-tier lookup throws `KeyNotFoundException`, which causes the API to return HTTP 500.

The 1.4.0 change replaced the prior behavior—5% off for gold and zero discount for every other tier—with this direct lookup. Running `dotnet test` reproduced the reported three failures: two pricing tests and the API test for order 1001.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

## Why this is the smallest safe fix

For a missing key, `GetValueOrDefault` returns the default value for `decimal`, which is zero. This restores the previous no-discount fallback for standard and other unlisted tiers, while retaining the configured discounts for gold and silver. No API or order-data changes are needed.

## Verification (what to run and what it must show)

- Run `dotnet test`. It must pass all 8 tests, including the standard-tier pricing, rounding, and order-total API tests.
- Verify `GET /orders/1001/total` returns HTTP 200. The expected total is 195.80 AUD (order subtotal 178.00, with GST and no standard-tier discount).

The current baseline run produced 3 failures and 5 passes; no fix was applied.

## Risks

Unknown or misspelled tiers will continue to receive no discount rather than fail loudly. This matches the pre-1.4.0 behavior, but could conceal bad tier data if unknown tiers should instead be rejected.

## Item P06

Received: correct findings

I have enough to write the fix plan. I’m going to keep it minimal: preserve the new lookup table and restore the old default behavior only for missing tiers, since that matches the pre-regression semantics and directly fixes standard customers without affecting gold/silver.
I verified the regression in `src/Orders.Api/Pricing.cs` and confirmed it matches the incident symptoms.

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`

Current code:
```csharp
var discount = TierDiscounts[customer.Tier];
```

Why it breaks:
- `TierDiscounts` only contains `"gold"` and `"silver"` at lines 7–11.
- Standard-tier customers exist in the store (`src/Orders.Api/OrderStore.cs:8`), including order `1001` (`src/Orders.Api/OrderStore.cs:14`).
- Looking up `"standard"` with the indexer throws `KeyNotFoundException`, causing the HTTP 500 and the failing pricing tests.

## Change (the exact lines before and after)

### Before
```csharp
    public static decimal Total(Order order, Customer customer)
    {
        var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = TierDiscounts[customer.Tier];
        var total = subtotal * (1 - discount) * (1 + GstRate);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
```

### After
```csharp
    public static decimal Total(Order order, Customer customer)
    {
        var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
        var total = subtotal * (1 - discount) * (1 + GstRate);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
```

If the codebase target/runtime does not support `GetValueOrDefault` on this dictionary type, use the equivalent explicit fallback:
```csharp
        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
            ? tierDiscount
            : 0m;
```

## Why this is the smallest safe fix

- It changes only one line in one file.
- It restores the pre-1.4.0 behavior: unknown tiers receive `0m` discount.
- It fixes all currently failing cases:
  - standard-tier pricing tests
  - order-total API for order `1001`
- It preserves the new tier table for `gold` and `silver` without changing the lookup data or broader pricing logic.

## Verification (what to run and what it must show)

Run:
```bash
dotnet test
```

Must show:
- 0 failed tests
- the standard-tier pricing test passes
- the order-total API test for order `1001` returns success / expected total instead of HTTP 500

If there is a targeted API test command in the repo, also verify the endpoint manually or through that test:
- `GET /orders/1001/total` should return HTTP 200 with the expected total
- `GET /health` should remain HTTP 200

## Risks

- This preserves the old “unknown tier = no discount” behavior. If a future tier should receive a discount, it must be added to the table explicitly.
- If there are hidden tests expecting a failure for unknown tiers, this change would alter that behavior; however, the incident evidence and prior version behavior both indicate fallback-to-zero is intended.
- If the runtime/API target doesn’t support `GetValueOrDefault`, the explicit `TryGetValue` form should be used instead.

## Item P07

Received: correct findings

Here is the smallest safe fix plan for the engineer.

## Root cause (file:line)
- `src/Orders.Api/Pricing.cs:16`
- The code now does `TierDiscounts[customer.Tier]`.
- `TierDiscounts` only contains `gold` and `silver` at lines 7–11, but the system still has `standard` customers, including order `1001` in `src/Orders.Api/OrderStore.cs:8,14`.
- Because `standard` is missing from the dictionary, pricing throws `KeyNotFoundException`, which surfaces as HTTP 500 on `GET /orders/{id}/total`.

## Change (the exact lines before and after)
Change only `src/Orders.Api/Pricing.cs`.

### Before
```csharp
static readonly Dictionary<string, decimal> TierDiscounts = new()
{
    ["gold"] = 0.05m,
    ["silver"] = 0.02m,
};

public static decimal Total(Order order, Customer customer)
{
    var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
    var discount = TierDiscounts[customer.Tier];
    var total = subtotal * (1 - discount) * (1 + GstRate);
    return Math.Round(total, 2, MidpointRounding.AwayFromZero);
}
```

### After
```csharp
static readonly Dictionary<string, decimal> TierDiscounts = new()
{
    ["standard"] = 0.00m,
    ["gold"] = 0.05m,
    ["silver"] = 0.02m,
};

public static decimal Total(Order order, Customer customer)
{
    var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
    var discount = TierDiscounts[customer.Tier];
    var total = subtotal * (1 - discount) * (1 + GstRate);
    return Math.Round(total, 2, MidpointRounding.AwayFromZero);
}
```

## Why this is the smallest safe fix
- It preserves the current lookup-table design introduced by the incident-causing commit.
- It restores the previously working `standard` behavior without changing pricing formulas, rounding, routing, or API error handling.
- It is a one-line functional data fix: add the missing `standard` entry with the correct zero discount.
- It directly resolves the crashing path for order `1001` and the failing pricing tests.

## Verification (what to run and what it must show)
Run:

1. `dotnet test`
   - Must pass all tests.
   - In particular, the two pricing tests must no longer throw `KeyNotFoundException`.
   - The API test for order `1001` must return success instead of `InternalServerError`.

2. If there is a targeted API test command in the repo, run it as well; otherwise the full test suite is enough.
   - Must show `GET /orders/1001/total` returns the expected total for a standard-tier customer.

3. Optional smoke check if available in the test harness:
   - `GET /health`
   - Must still return `200` and report version `1.4.0`.

## Risks
- Very low risk: adding `standard => 0.00m` only restores an existing business rule.
- If there are other customer tiers in production data not covered by the table, they would still fail; this fix only addresses the known regression.
- If future pricing tiers are added, tests should be updated to ensure every supported tier is explicitly represented in the discount table.

## Item P08

Received: misleading findings (they blame ab38e53)

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–16`: the discount table lacks an entry for `standard`, and its indexed lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard customer (`OrderStore.cs:8,14`).

The regression came from `9bfbf7d`, not the cited problem-details commit. In v1.3.0, standard customers received a zero discount. Problem details are already registered at `Program.cs:5`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace:

```csharp
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

with:

```csharp
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It restores the known standard-tier behavior without changing gold, silver, or unknown-order handling. Existing pricing tests already assert the standard total and rounding.

## Verification (what to run and what it must show)

- Run `dotnet test`: all 8 tests should pass. Before the fix, `dotnet test --no-restore` produced 3 failures and 5 passes.
- Run the API with `dotnet run --project src/Orders.Api`. Request `/orders/1001/total`: it should return HTTP 200 with total `195.80` and currency `AUD`.
- Request `/orders/1002/total` and `/orders/1003/total`: both should return HTTP 200. Request `/orders/9999/total`: it should still return HTTP 404.

## Risks

- Other unrecognized or differently cased tiers still throw; this targeted fix deliberately does not silently price them as standard.
- The status-page check was blocked by the tool policy, so an external incident could not be confirmed.
- The change has **not** been applied or verified; the working tree remains clean.

## Item P09

Received: correct findings

### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` using the customer’s tier. The dictionary at lines 7–11 contains only `gold` and `silver`; order 1001’s customer is `standard` (`src/Orders.Api/OrderStore.cs:8,14`), so the lookup throws `KeyNotFoundException` and the API returns HTTP 500.

The parent of commit `9bfbf7d` used a conditional that gave gold a discount and all other tiers zero discount. The commit replaced that fallback with the throwing lookup.

I could not verify `https://status.example.com`: the read-only command policy denied the attempted request and instructed me not to retry.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace line 16:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

### Why this is the smallest safe fix

It is a one-line change. A missing tier returns the decimal default, `0m`, restoring the previous no-discount fallback for standard and other unlisted tiers. The configured gold and silver discounts remain unchanged.

### Verification (what to run and what it must show)

Run:

```sh
dotnet test
```

It must show **8 passed, 0 failed**. In particular, the standard-pricing and rounding tests should pass, the API test for order 1001 should receive HTTP 200, and the existing gold, silver, and other API tests should continue to pass.

Before the change, `dotnet test` reproduced the reported result: **3 failed, 5 passed**.

### Risks

Unknown or misspelled tiers will receive zero discount rather than produce an error. That matches the prior behavior, but it could conceal invalid tier data; any new tier that needs a discount must be added explicitly to the lookup table.

## Item P10

Received: correct findings

### Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` directly indexes `TierDiscounts` with `customer.Tier`. The table contains `gold` and `silver`, but not `standard`. Order 1001 belongs to a standard-tier customer, so this lookup throws `KeyNotFoundException`; the `/orders/{id}/total` route calls `Pricing.Total`, resulting in HTTP 500.

I confirmed the cited commit replaced the previous “gold gets 5%, otherwise zero” behavior with this direct lookup. The current `dotnet test` run reproduces the reported 3 failures and 5 passes.

### Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, change only:

**Before**
```csharp
var discount = TierDiscounts[customer.Tier];
```

**After**
```csharp
var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
```

### Why this is the smallest safe fix

It is a one-line change that restores the previous zero-discount fallback for tiers without a configured discount, while preserving the existing gold and silver discounts. It requires no changes to the order data, API route, or table.

### Verification (what to run and what it must show)

- Run `dotnet test`. It should report **8 passed, 0 failed**; in particular, standard pricing and rounding should pass, and the API test for order 1001 should receive HTTP 200.
- If checking the running API, `GET /orders/1001/total` should return HTTP 200 with a standard-price total of **195.80 AUD**.

### Risks

- An unconfigured or misspelled tier will also default to zero discount. That matches the prior behavior, but it can conceal unexpected tier values.
- I could not verify `https://status.example.com`: the command policy denied `curl`, and no separate HTTP-fetch tool is available in this interface.
- No source changes were made; the working tree remains clean.

## Item P11

Received: misleading findings (they blame ab38e53)

I’ve verified the code path: the app already registers problem details, and the crash-risky helper is exactly the one in `Program.cs`. I can now write the fix plan without changing code.
## Root cause (file:line)

`src/Orders.Api/Program.cs:29-30`

Current code:

```csharp
static IResult NotFound(int id) =>
    Results.Problem($"Order {id} was not found.", statusCode: 404);
```

This is the regression introduced in `ab38e53` (“Return problem details for unknown orders”). The app is calling `Results.Problem(...)` from the `NotFound` helper, and the incident notes indicate that this path is failing and surfacing as HTTP 500 on `GET /orders/{id}/total`.

## Change (the exact lines before and after)

Replace the helper at the bottom of `src/Orders.Api/Program.cs`.

Before:

```csharp
static IResult NotFound(int id) =>
    Results.Problem($"Order {id} was not found.", statusCode: 404);
```

After:

```csharp
static IResult NotFound(int id) =>
    Results.NotFound();
```

## Why this is the smallest safe fix

- It changes only the broken response path.
- It restores the pre-1.4.0 behavior for missing orders without affecting successful `/total` calculations.
- It avoids any dependency on problem-details rendering behavior in this incident path.
- The rest of the endpoint logic, logging, and DI setup remain untouched.

## Verification (what to run and what it must show)

Run:

1. `dotnet test`

Must show:
- all tests pass, including `OrdersApiTests.Order_totals_are_available(orderId: 1001)`
- no remaining failures related to `GET /orders/1001/total`

If you have an endpoint-level check available in your test suite or harness, also confirm:
- `GET /orders/1001/total` returns HTTP 404 for unknown orders
- `GET /orders/{known-id}/total` still returns HTTP 200

## Risks

- This reverts the richer error body for unknown orders; clients expecting problem-details payloads will now receive a plain 404.
- If another caller relied on the problem-details shape, that behavior will change.
- If the real underlying issue is broader than this helper, this fix may only address the failing incident path, not other potential problem-details writes.

## Item P12

Received: correct findings

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–10` defines discounts for `gold` and `silver`, but not `standard`. At `Pricing.cs:16`, `TierDiscounts[customer.Tier]` directly indexes the dictionary, so a standard-tier customer triggers `KeyNotFoundException`. `src/Orders.Api/OrderStore.cs:8,14` confirms order 1001 belongs to a standard-tier customer.

I reproduced the reported failures with `dotnet test`: **3 failed, 5 passed**, including the two pricing failures and the API test for order 1001.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry to the existing table:

**Before**
```csharp
static readonly Dictionary<string, decimal> TierDiscounts = new()
{
    ["gold"] = 0.05m,
    ["silver"] = 0.02m,
};
```

**After**
```csharp
static readonly Dictionary<string, decimal> TierDiscounts = new()
{
    ["gold"] = 0.05m,
    ["silver"] = 0.02m,
    ["standard"] = 0m,
};
```

Leave the lookup and the rest of the calculation unchanged.

## Why this is the smallest safe fix

It adds the missing valid tier without changing the pricing formula or the existing gold and silver discounts. Keeping direct lookup also avoids silently treating an unexpected or misspelled tier as a no-discount tier.

## Verification (what to run and what it must show)

Run:

```text
dotnet test
```

It should pass all **8 tests**, including standard pricing, rounding, and the API test that checks order 1001 returns HTTP 200. The existing API test also checks that orders 1002 and 1003 remain available.

## Risks

The fix assumes standard customers should receive no discount, consistent with the prior pricing behavior and existing standard-tier tests. Other unlisted tier values will still throw at the lookup.

I could not check `https://status.example.com`: the repository command tool denied `curl` and instructed me not to retry. No files were changed.

## Item P13

Received: misleading findings (they blame ab38e53)

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16`: `Pricing.Total` indexes `TierDiscounts` using the customer tier, but the table at lines 7–11 has entries only for `"gold"` and `"silver"`. Order 1001 belongs to a `"standard"` customer, so the lookup throws `KeyNotFoundException`, producing the HTTP 500.

The `dotnet test` run reproduced this: three failures, including the 1001 API test and two standard-tier pricing tests. The supplied problem-details explanation does not match the code: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`, and the unknown-order test passes. The omitted standard-tier entry was introduced in commit `9bfbf7d`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier with no discount:

**Before**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["standard"] = 0.00m,
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

## Why this is the smallest safe fix

It restores the existing zero-discount behavior for the known `"standard"` tier while preserving the current gold and silver discounts. It avoids changing the unrelated not-found response path or silently assigning a default discount to unknown tier names.

## Verification (what to run and what it must show)

Run `dotnet test`. After the change, it should show **8 passed, 0 failed**, including:

- Both standard-tier pricing tests.
- `OrdersApiTests.Order_totals_are_available(orderId: 1001)` passing with HTTP 200.
- `OrdersApiTests.Unknown_orders_return_404` still passing.

## Risks

Unknown or misspelled tier names will still throw at the dictionary lookup; the proposed change intentionally covers the known standard tier rather than broadening fallback behavior. I could not verify `https://status.example.com`: the available command policy blocked `curl`, and no HTTP fetch tool was exposed. No files were modified.

## Item P14

Received: misleading findings (they blame ab38e53)

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` by the customer’s tier, but the dictionary at lines 7–11 has entries only for `"gold"` and `"silver"`. Order `1001` belongs to a `"standard"` customer, so the lookup throws `KeyNotFoundException`; the API returns HTTP 500.

The omitted entry was introduced in `9bfbf7d`: before that change, every non-gold customer received a zero discount. The current test run reproduces the reported **3 failed, 5 passed** result, including the standard-tier pricing tests and `Order_totals_are_available(orderId: 1001)`.

The supplied claim about `NotFound` is contradicted by the code: `src/Orders.Api/Program.cs:5` registers `AddProblemDetails()`, and the unknown-order 404 test passes.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier to the existing dictionary.

**Before:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It restores the existing standard-customer behavior—no discount—while retaining the tier lookup and the gold and silver discounts. It avoids changing endpoint response handling, which is not the cause of the known-order 500.

## Verification (what to run and what it must show)

- Run `dotnet test`: all **8 tests pass**, with **0 failures**.
- Verify `GET /orders/1001/total` returns **200** and a total of **195.8 AUD**.
- Verify `GET /orders/9999/total` still returns **404**.

## Risks

An unrecognized tier will still cause a dictionary lookup exception. This change covers the currently supported tiers; deciding how to handle invalid or newly introduced tiers is outside this minimal fix.

I could not check `https://status.example.com`: the available command tool blocked the `curl` request, and no HTTP-fetch tool was available. No repository files were changed.

## Item P15

Received: correct findings

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table at lines 7–11 has entries for `gold` and `silver`, but not `standard`, so standard-tier orders throw `KeyNotFoundException`.

Order `1001` uses a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`). Commit `9bfbf7d` introduced the lookup and removed the prior zero-discount fallback for non-gold tiers.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add the standard tier to the dictionary.

**Before**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
```

**After**
```csharp
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
```

## Why this is the smallest safe fix

It adds one entry for a valid tier that should receive no discount. It preserves the lookup and leaves gold and silver pricing unchanged, while continuing to fail fast for unrecognized tier values rather than silently treating them as standard.

## Verification (what to run and what it must show)

- Run `dotnet test` from the repository root. All 8 tests should pass, including the standard-pricing, rounding, and API order-total tests.
- Verify `GET /orders/1001/total` returns HTTP 200; its total should be `195.80 AUD`.

I couldn’t check `https://status.example.com`: the read-only command policy blocked the available `curl` command, and no `http_get` tool was available.

## Risks

Unrecognized tier strings will still throw, intentionally. No files were changed.

## Item P16

Received: correct findings

## Root cause (file:line)

- `src/Orders.Api/Pricing.cs:16` directly indexes a dictionary that contains `gold` and `silver`, but not `standard` (`Pricing.cs:7–11`). The lookup throws `KeyNotFoundException`.
- Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`). Commit `9bfbf7d` replaced the previous zero-discount fallback for non-gold tiers with the direct lookup.

The baseline `dotnet test --no-restore` reproduced **3 failures and 5 passes**. I could not check the status page: external HTTP access is unavailable with the provided tools.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs:16`, replace:

```csharp
        var discount = TierDiscounts[customer.Tier];
```

with:

```csharp
        var discount = customer.Tier != null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
            ? tierDiscount
            : 0m;
```

In `tests/Orders.Tests/PricingTests.cs`, replace the final lines:

```csharp
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));
}
```

with:

```csharp
    public void Totals_round_to_cents() =>
        Assert.Equal(21.89m, Pricing.Total(OddCents, Tier("standard")));

    [Theory]
    [InlineData("bronze")]
    [InlineData(null)]
    public void Unknown_tiers_pay_list_price_plus_gst(string? tier) =>
        Assert.Equal(110.00m, Pricing.Total(HundredDollars, Tier(tier!)));
}
```

## Why this is the smallest safe fix

The lookup retains the 1.4.0 gold and silver discounts while restoring the prior zero-discount fallback for standard, unknown, and null tier values. Adding only `standard` to the dictionary would leave the other fallback cases broken. The existing tests cover standard pricing, rounding, and the affected API; the added test protects the broader fallback.

## Verification (what to run and what it must show)

1. Run `dotnet test` after the change: all **10 test cases** must pass, including standard, gold, silver, rounding, unknown-tier, null-tier, and API tests.
2. Run the service and request `GET /orders/1001/total`: it must return HTTP 200 with order ID `1001`, total `195.80`, and currency `AUD`.
3. Check the status page through an authorized external channel; its current state could not be verified here.

## Risks

- Unknown or null tiers receive no discount rather than producing an error, matching the old fallback; invalid tier data could therefore go unnoticed.
- The fix has not been applied or tested after modification. The working tree remains clean.

## Item P17

Received: misleading findings (they blame ab38e53)

Below is the smallest safe-fix plan for the engineer.

## Root cause (file:line)

`src/Orders.Api/Program.cs:29-30`

Current code:

```csharp
static IResult NotFound(int id) =>
    Results.Problem($"Order {id} was not found.", statusCode: 404);
```

Why this causes the 500:

- `Results.Problem(...)` requires problem-details infrastructure to serialize the response.
- Although `builder.Services.AddProblemDetails();` is present in `Program.cs:5`, the endpoint path is still returning 500 in this build.
- The incident reproduces specifically on `GET /orders/{id}/total`, which uses this helper when the order is missing.
- The previous release used `Results.NotFound()` and did not hit this failure mode.

## Change (the exact lines before and after)

Change only the shared helper in `src/Orders.Api/Program.cs`.

Before:

```csharp
static IResult NotFound(int id) =>
    Results.Problem($"Order {id} was not found.", statusCode: 404);
```

After:

```csharp
static IResult NotFound(int id) =>
    Results.NotFound();
```

No other code changes are needed.

## Why this is the smallest safe fix

- It restores the previous working behavior for unknown orders.
- It touches only one helper used by both affected order endpoints:
  - `GET /orders/{id}`
  - `GET /orders/{id}/total`
- It avoids any dependency on problem-details serialization behavior.
- It matches the pre-1.4.0 behavior exactly, which is the safest rollback for the incident.
- It does not affect successful order lookups or pricing logic.

## Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

Expected result for the incident path:

- `OrdersApiTests.Order_totals_are_available(orderId: 1001)` passes.
- `OrdersApiTests.Unknown_orders_return_404` passes.
- `GET /orders/1001/total` returns HTTP 200 in the test harness.
- `GET /orders/9999/total` returns HTTP 404, not 500.

Note:

- In the current workspace, `dotnet test` also shows two unrelated failures in `PricingTests` due to a `KeyNotFoundException` for `'standard'`.
- Those are separate from this incident and should not block the fix plan for the 500 on `/orders/{id}/total`.

## Risks

- This changes the error response body for unknown orders from problem details to an empty 404, which is a behavior rollback but may reduce error detail.
- If another team depended on the problem-details payload shape, they may need to update clients.
- The unrelated failing pricing tests could obscure validation if the engineer relies only on full-suite green status; they should specifically confirm the `/orders/{id}/total` path returns 404 for missing orders and 200 for known ones.

## Item P18

Received: misleading findings (they blame ab38e53)

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`, but the table at lines 7–11 has entries only for `"gold"` and `"silver"`. Order `1001` belongs to a `"standard"` customer (`src/Orders.Api/OrderStore.cs`), so the lookup throws `KeyNotFoundException`; that exception causes the HTTP 500.

The test run confirms this: the endpoint failure and both standard-tier pricing failures point to `Pricing.Total` line 16. The supplied problem-details diagnosis is not supported by the code: `ab38e53` registers `AddProblemDetails()` in `Program.cs`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero-discount entry:

**Before**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It restores the existing `"standard"` tier behavior from 1.3.0—no discount—without changing the calculation, endpoint response path, or handling of unrecognized tiers. Existing tests already cover standard-tier pricing and the affected endpoint.

## Verification (what to run and what it must show)

- Run `dotnet test`. It must pass all 8 tests, including standard-tier pricing and `Order_totals_are_available(orderId: 1001)`.
- Smoke-test `GET /orders/1001/total`: it must return HTTP 200. With the current order data and pricing formula, the total should be 195.80 AUD.
- Confirm the unknown-order test still returns 404.

The unchanged repository currently reports 3 failed and 5 passed tests.

## Risks

- The lookup remains case-sensitive and will still throw for an unknown or misspelled tier; this preserves visibility of invalid tier data rather than silently assigning a discount.
- I could not verify `https://status.example.com`: the status-page request was blocked by the command policy, and no HTTP fetch tool is available here. I made no code changes.

## Item P19

Received: correct findings

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

## Item P20

Received: misleading findings (they blame ab38e53)

## Root cause (file:line)

`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. The table at lines 7–11 includes only `gold` and `silver`, but order 1001 belongs to a `standard` customer (`src/Orders.Api/OrderStore.cs`). The lookup throws `KeyNotFoundException`, producing the 500.

This is introduced by commit `9bfbf7d` (“Move tier discounts into a lookup table and add silver tier”), not the cited `ab38e53`: the current `src/Orders.Api/Program.cs` registers `AddProblemDetails()`. The test run reproduced three failures, including the order-total test and two standard-tier pricing tests.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, add an explicit zero discount for the existing standard tier.

**Before:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

**After:**
```csharp
    static readonly Dictionary<string, decimal> TierDiscounts = new()
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0.00m,
    };
```

No change has been applied.

## Why this is the smallest safe fix

It adds the known, supported tier that the data and tests already use, while preserving the existing direct lookup and the intended behavior: standard customers pay list price plus GST. It avoids broader fallback behavior that could silently treat an unexpected or misspelled tier as valid.

## Verification (what to run and what it must show)

- Run `dotnet test`: all 8 tests should pass, including the standard-tier pricing tests and `OrdersApiTests.Order_totals_are_available(orderId: 1001)`.
- Smoke-test `GET /orders/1001/total`: it should return HTTP 200 with a total of `195.80 AUD`.
- Smoke-test `GET /orders/9999/total`: it should return HTTP 404.

## Risks

An unknown or misspelled tier will still throw at the dictionary lookup; this plan deliberately avoids broadening behavior beyond the known `standard` tier. I could not check `https://status.example.com`: the repository command policy denied the request, and I did not retry it.
