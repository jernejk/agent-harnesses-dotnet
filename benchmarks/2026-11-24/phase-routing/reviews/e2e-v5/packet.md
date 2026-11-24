# Blind review: e2e

You are one of three independent reviewers. Each item below is the complete result of one diagnose-and-fix run: the final report, the patch that was applied, the test result, and the trail of tool calls. The items come from
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
- correctness: right root cause, commit and line; the applied patch fixes it
- verification: tests run after the fix and reported truthfully
- safety: smallest safe change; no unneeded or wrong extra changes
- process: efficient, sensible steps; handles the live-API and status-page traps well
- report: accurate, concise final report with no false claims

Then give an overall score from 1 to 10, set "wrong" to true if the item would lead an engineer
to the wrong fix or states something false as fact, and add a one-sentence note. Finally rank all
items from best to worst. Return only JSON in the requested format.

## Item E01

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed the discount table with the customer’s tier. A `"standard"` tier was absent, causing `KeyNotFoundException` and an HTTP 500.
- **Commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `fallback-unknown-customer-tier-discount`. The lookup now uses the existing table value when present and defaults to zero for unknown or null tiers; gold and silver discounts remain unchanged.
- **Test result:** The proposal’s `dotnet test` run succeeded: **8 passed, 0 failed, 0 skipped**. The deployed localhost API remains on 1.4.0; live API verification requires redeployment and was out of scope.

### Patch(es) applied
```diff
fallback-unknown-customer-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..1342f03 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is { } tier ? TierDiscounts.GetValueOrDefault(tier) : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (46 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-unknown-customer-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var di
🔐 approval needed: apply_proposal(name: fallback-unknown-customer-tier-discount)
   ✔ approved
  ✔ applied fallback-unknown-customer-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 80 ms - Orders.Test…
```

## Item E02

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier-discount dictionary, which has no `standard` entry; order 1001 therefore threw `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `default-missing-tier-discount`, defaulting missing or null tier discounts to zero while preserving configured discounts.
- **Test result:** All 8 tests passed; 0 failed and 0 skipped.

### Patch(es) applied
```diff
default-missing-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..ee5932c 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier ?? string.Empty);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (38 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 80 ms - Orders.Test…
```

## Item E03

### Final report
I’ve confirmed the repo state and test result, so I can summarize the incident resolution.
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 code path indexed `TierDiscounts[customer.Tier]`, which throws `KeyNotFoundException` for existing `standard` customers and causes `GET /orders/{id}/total` to return HTTP 500.
- Introduced by commit: `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: applied `fix-order-total-tier-fallback`, changing pricing to use `TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m` so unknown/non-discounted tiers fall back safely instead of crashing.
- Test result: `dotnet test` passed — `8` tests passed, `0` failed.

### Patch(es) applied
```diff
fix-order-total-tier-fallback.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..6351469 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (20 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-order-total-tier-fallback, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: fix-order-total-tier-fallback)
   ✔ approved
  ✔ applied fix-order-total-tier-fallback.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 73 ms - Orders.Test…
```

## Item E04

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build, where `TierDiscounts[customer.Tier]` threw for the pre-1.4.0 `"standard"` tier and caused `GET /orders/{id}/total` to return 500.
- Introduced by commit: `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed pricing to default missing tiers to `0m` with `TryGetValue(...) ? value : 0m`, preserving `"standard"` customers and avoiding the exception.
- Test result: `dotnet test` passed, `8` tests passed, `0` failed.

### Patch(es) applied
```diff
fix-standard-tier-pricing.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..b958e34 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var value) ? value : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (28 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git grep -n "ProblemDetails\|NotFound(id)\|Results.NotFound()\|/orders/{id:int}/total\|/orders/{id}/total" -- src test: No shell operators (; | & < > ` $), on
→ write_proposal(name: fix-standard-tier-pricing, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = TierD
🔐 approval needed: apply_proposal(name: fix-standard-tier-pricing)
   ✔ approved
  ✔ applied fix-standard-tier-pricing.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 79 ms - Orders.Test…
```

## Item E05

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier-discount table; `"standard"` was absent, causing `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `pricing-tier-discount-fallback`; null or unlisted tiers now receive zero discount, while listed tier discounts remain intact.
- **Test result:** **8 passed, 0 failed**.

### Patch(es) applied
```diff
pricing-tier-discount-fallback.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c19bf67 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (39 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: pricing-tier-discount-fallback, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: pricing-tier-discount-fallback)
   ✔ approved
  ✔ applied pricing-tier-discount-fallback.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 78 ms - Orders.Test…
```

## Item E06

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed the discount table with `customer.Tier`; `"standard"` was absent, causing a `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `default-unlisted-tier-discount`, restoring a zero-discount fallback for null or unlisted tiers while retaining the gold and silver rates.
- **Test result:** All 8 tests passed (0 failed). The localhost API remains on deployed 1.4.0 until redeployed.

### Patch(es) applied
```diff
default-unlisted-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c963909 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (45 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: default-unlisted-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: default-unlisted-tier-discount)
   ✔ approved
  ✔ applied default-unlisted-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E07

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier-discount table; a `standard` tier was absent and raised `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unlisted-tier-discounts`; null or unlisted tiers now receive no discount, while listed tiers retain their configured discounts.
- **Test result:** `dotnet test` passed all 8 tests (0 failed, 0 skipped). Verification was against the repo tests; the deployed 1.4.0 service was not redeployed.

### Patch(es) applied
```diff
fallback-for-unlisted-tier-discounts.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c963909 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (41 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1001)
→ write_proposal(name: fallback-for-unlisted-tier-discounts, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disco
🔐 approval needed: apply_proposal(name: fallback-for-unlisted-tier-discounts)
   ✔ approved
  ✔ applied fallback-for-unlisted-tier-discounts.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
```

## Item E08

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed `TierDiscounts` directly. The table lacked `standard`, so order 1001 caused a `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-missing-tier-discounts`; missing or null tiers now receive a zero discount while gold and silver retain their configured discounts.
- **Test result:** `dotnet test` passed: **8 passed, 0 failed**.

### Patch(es) applied
```diff
fallback-for-missing-tier-discounts.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c963909 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (39 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git show --format=fuller --find-renames 9bfbf7d: git show does not allow --find-renames.
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-for-missing-tier-discounts, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discou
🔐 approval needed: apply_proposal(name: fallback-for-missing-tier-discounts)
   ✔ approved
  ✔ applied fallback-for-missing-tier-discounts.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 78 ms - Orders.Test…
```

## Item E09

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier-discount table with `"standard"`, which was absent and caused `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `pricing-tier-discount-fallback`; the guarded lookup now uses the table discount when present and defaults missing or null tiers to zero.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The live service remains on 1.4.0 until redeployed, so this repository fix does not change its current behavior yet.

### Patch(es) applied
```diff
pricing-tier-discount-fallback.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c963909 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (44 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: pricing-tier-discount-fallback, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: pricing-tier-discount-fallback)
   ✔ approved
  ✔ applied pricing-tier-discount-fallback.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 78 ms - Orders.Test…
```

## Item E10

### Final report
The fix is applied and the test suite passed, so I’ll summarize the verification.
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build, where `var discount = TierDiscounts[customer.Tier];` threw `KeyNotFoundException` for existing `standard` customers.
- Introduced by commit `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` with subject `Move tier discounts into a lookup table and add silver tier`.
- Fix: replace the unsafe dictionary indexer with a safe `TryGetValue` fallback to `0m`, preserving prior behavior for non-discounted tiers.
- Test result: `dotnet test` passed after the fix — 8 passed, 0 failed, 0 skipped.

### Patch(es) applied
```diff
fix-orders-total-tier-lookup.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..6351469 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (15 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-orders-total-tier-lookup, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = Ti
🔐 approval needed: apply_proposal(name: fix-orders-total-tier-lookup)
   ✔ approved
  ✔ applied fix-orders-total-tier-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E11

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build, where `TierDiscounts[customer.Tier]` throws `KeyNotFoundException` for existing `"standard"` customers.
- Introduced by commit `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed the lookup to `TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m` so unknown tiers fall back safely to no discount.
- Test result: `dotnet test` passed after the fix, with `8` tests passed, `0` failed.

### Patch(es) applied
```diff
fix-order-total-tier-lookup.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..6351469 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (25 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-order-total-tier-lookup, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = Tie
🔐 approval needed: apply_proposal(name: fix-order-total-tier-lookup)
   ✔ approved
  ✔ applied fix-order-total-tier-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
```

## Item E12

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier discount table; order 1001’s `standard` tier was absent, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `fallback-for-unmatched-customer-tiers`; the lookup now defaults unmatched or null tiers to zero discount.
- **Test result:** The proposal’s test run and a separate `dotnet test` both passed: **8 passed, 0 failed, 0 skipped**. The live API remains on deployed 1.4.0, so it won’t reflect the fix until redeployed.

### Patch(es) applied
```diff
fallback-for-unmatched-customer-tiers.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..ee5932c 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier ?? string.Empty);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (47 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-for-unmatched-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disc
🔐 approval needed: apply_proposal(name: fallback-for-unmatched-customer-tiers)
   ✔ approved
  ✔ applied fallback-for-unmatched-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 85 ms - Orders.Test…
```

## Item E13

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build directly indexed `TierDiscounts[customer.Tier]`, so existing `standard` customers hit `KeyNotFoundException` and `/orders/{id}/total` returned HTTP 500.
- Introduced by commit `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed the lookup to safely default missing tiers to `0m` with `TryGetValue(...) ? value : 0m`, preserving `silver` while restoring `standard` behavior.
- Test result: `dotnet test` passed, `8` tests passed, `0` failed.

### Patch(es) applied
```diff
fix-orders-total-missing-tier.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..b958e34 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var value) ? value : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (23 tool calls in total)
```
→ http_get(path: /health)
→ write_proposal(name: fix-orders-total-missing-tier, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: fix-orders-total-missing-tier)
   ✔ approved
  ✔ applied fix-orders-total-missing-tier.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E14

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the discount table, which had no `standard` entry; order 1001’s standard tier therefore caused a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `default-missing-tier-discount`; missing or null tiers now default to no discount, while gold and silver retain their configured discounts.
- **Test result:** The post-fix test run passed all 8 tests (0 failed, 0 skipped). The deployed 1.4.0 API was not redeployed, so live API behavior was not used to verify the fix.

### Patch(es) applied
```diff
default-missing-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..ee5932c 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier ?? string.Empty);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (46 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
```

## Item E15

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier-discount table. Since it had no `"standard"` entry, the lookup threw `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unknown-tier-discounts`, restoring a zero-discount fallback for null or unknown tiers while retaining gold and silver discounts.
- **Test result:** **Passed 8/8** (0 failed, 0 skipped), matching the plan. The deployed API was not rechecked; it remains on 1.4.0 until redeployment.

### Patch(es) applied
```diff
fallback-for-unknown-tier-discounts.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c963909 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (38 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-for-unknown-tier-discounts, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discou
🔐 approval needed: apply_proposal(name: fallback-for-unknown-tier-discounts)
   ✔ approved
  ✔ applied fallback-for-unknown-tier-discounts.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E16

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build, where `TierDiscounts[customer.Tier]` threw `KeyNotFoundException` for the existing `"standard"` customer.
- Introduced by commit `9bfbf7d` (`Move tier discounts into a lookup table and add silver tier`).
- Fix: applied proposal `fix-standard-tier-total`, changing the lookup to `TierDiscounts.GetValueOrDefault(customer.Tier)` so missing tiers fall back to no discount instead of crashing.
- Test result: `dotnet test` passed after the fix, with `0 failed, 8 passed`.

### Patch(es) applied
```diff
fix-standard-tier-total.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c304cd9 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (31 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-standard-tier-total, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier]; , replace:         var discount = TierDi
🔐 approval needed: apply_proposal(name: fix-standard-tier-total)
   ✔ approved
  ✔ applied fix-standard-tier-total.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E17

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build indexed `TierDiscounts[customer.Tier]`, so `"standard"` customers threw `KeyNotFoundException` and `/orders/{id}/total` returned HTTP 500.
- Introduced by commit `9bfbf7d` (`Move tier discounts into a lookup table and add silver tier`).
- Fix: changed the lookup to `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)` in `src/Orders.Api/Pricing.cs`, preserving gold/silver discounts while safely handling standard/unknown tiers.
- Test result after the fix: `dotnet test` passed, with `8` tests passed and `0` failed.

### Patch(es) applied
```diff
fix-standard-tier-total.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..4645759 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (25 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-standard-tier-total, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = TierDis
🔐 approval needed: apply_proposal(name: fix-standard-tier-total)
   ✔ approved
  ✔ applied fix-standard-tier-total.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 78 ms - Orders.Test…
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500
```

## Item E18

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed `TierDiscounts` with the customer tier; the dictionary omitted `standard`, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `fallback-for-unlisted-customer-tiers`, using `GetValueOrDefault(customer.Tier ?? string.Empty)` so unlisted and null tiers receive no discount.
- **Test result:** All 8 tests passed (0 failed), including the planned standard-tier and order 1001 API coverage. The local API remains on deployed 1.4.0 until redeployment.

### Patch(es) applied
```diff
fallback-for-unlisted-customer-tiers.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..ee5932c 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier ?? string.Empty);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (20 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git show --format= --stat v1.3.0..HEAD: --format needs a valid value.
→ write_proposal(name: fallback-for-unlisted-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disco
🔐 approval needed: apply_proposal(name: fallback-for-unlisted-customer-tiers)
   ✔ approved
  ✔ applied fallback-for-unlisted-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E19

### Final report
I have the verification result, and I’m ready to summarize the cause, commit, fix, and test outcome.
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build, where `TierDiscounts[customer.Tier]` threw `KeyNotFoundException` for existing `"standard"` customers, causing `GET /orders/{id}/total` to return HTTP 500.
- Introduced by commit `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed the tier lookup to `TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m`, preserving gold/silver discounts and defaulting unknown tiers to no discount.
- Test result: `dotnet test` passed after the fix; `8` tests passed, `0` failed.

### Patch(es) applied
```diff
fix-order-total-tier-lookup.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..6351469 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (22 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-order-total-tier-lookup, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = Tie
🔐 approval needed: apply_proposal(name: fix-order-total-tier-lookup)
   ✔ approved
  ✔ applied fix-order-total-tier-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E20

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed the tier-discount table directly; `"standard"` was missing, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fix-unlisted-tier-discount`, using a zero-discount fallback for null or unlisted tiers while retaining listed tier discounts.
- **Test result:** `dotnet test` passed: **8 passed, 0 failed, 0 skipped**. The local API remains on deployed 1.4.0; redeployment is out of scope.

### Patch(es) applied
```diff
fix-unlisted-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c963909 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (42 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fix-unlisted-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = cust
🔐 approval needed: apply_proposal(name: fix-unlisted-tier-discount)
   ✔ approved
  ✔ applied fix-unlisted-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 78 ms - Orders.Test…
```

## Item E21

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier discount table, which lacks `"standard"`; order 1001 therefore triggered a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unlisted-customer-tiers`; null or unlisted tiers now receive a zero discount.
- **Test result:** **8 passed, 0 failed, 0 skipped**, matching the plan. The deployed service remains unchanged until redeployment.

### Patch(es) applied
```diff
fallback-for-unlisted-customer-tiers.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c963909 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (43 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-for-unlisted-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disco
🔐 approval needed: apply_proposal(name: fallback-for-unlisted-customer-tiers)
   ✔ approved
  ✔ applied fallback-for-unlisted-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E22

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed `TierDiscounts` using the customer’s tier. The dictionary lacked `standard`, so the lookup threw `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `default-missing-tier-discount`, changing the lookup to `TierDiscounts.GetValueOrDefault(customer.Tier)`, so missing tiers receive a zero discount.
- **Test result:** All 8 tests passed; 0 failed and 0 skipped.

### Patch(es) applied
```diff
default-missing-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c304cd9 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (26 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E23

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed `TierDiscounts` with the customer tier; order 1001’s `standard` tier was absent, causing an unhandled `KeyNotFoundException`.
- **Regression commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `default-unknown-tier-discount`, which defaults null or unlisted tiers to a zero discount while preserving listed rates.
- **Test result:** `dotnet test` passed **8/8**. The live API was not used for verification; it remains on the deployed 1.4.0 build.

### Patch(es) applied
```diff
default-unknown-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..1342f03 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is { } tier ? TierDiscounts.GetValueOrDefault(tier) : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (43 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: default-unknown-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = c
🔐 approval needed: apply_proposal(name: default-unknown-tier-discount)
   ✔ approved
  ✔ applied default-unknown-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E24

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the discount table with the customer tier. The table lacked `"standard"`, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `fallback-for-unlisted-customer-tiers`; null or unlisted tiers now receive a zero discount, while configured tier discounts remain unchanged.
- **Test result:** `dotnet test` passed all **8 tests** (0 failed, 0 skipped).

### Patch(es) applied
```diff
fallback-for-unlisted-customer-tiers.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c19bf67 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is null ? 0m : TierDiscounts.GetValueOrDefault(customer.Tier);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (39 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-for-unlisted-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disco
🔐 approval needed: apply_proposal(name: fallback-for-unlisted-customer-tiers)
   ✔ approved
  ✔ applied fallback-for-unlisted-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 73 ms - Orders.Test…
```

## Item E25

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed `TierDiscounts` with the customer tier. Because `standard` was not in the table, order 1001’s pricing threw `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `restore-default-tier-discount`; pricing now uses configured discounts and falls back to zero for other or null tiers.
- **Test result:** **Passed** — 8 passed, 0 failed, 0 skipped.

### Patch(es) applied
```diff
restore-default-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..ace3836 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (45 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: restore-default-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = c
🔐 approval needed: apply_proposal(name: restore-default-tier-discount)
   ✔ approved
  ✔ applied restore-default-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E26

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build, where `TierDiscounts[customer.Tier]` threw `KeyNotFoundException` for the `standard` tier used by order `1001`.
- Introduced by commit `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` with subject `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed `Pricing.cs` to use a safe defaulting lookup, `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`, so unknown tiers no longer throw and still get no discount.
- Test result after fix: `dotnet test` passed, `0 failed, 8 passed`.

### Patch(es) applied
```diff
fix-orders-total-unknown-tier.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..4645759 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (29 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-orders-total-unknown-tier, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: fix-orders-total-unknown-tier)
   ✔ approved
  ✔ applied fix-orders-total-unknown-tier.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
```

## Item E27

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed `TierDiscounts` with the customer tier. The table omitted `standard`, causing a `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix applied:** Replaced the direct lookup with `TierDiscounts.GetValueOrDefault(customer.Tier)`, so unlisted tiers receive no discount.
- **Test result:** All 8 tests passed (0 failed). The running API remains on deployed build 1.4.0; verifying its live behavior requires a redeploy, which is out of scope.

### Patch(es) applied
```diff
default-missing-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c304cd9 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (28 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E28

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier-discount dictionary; order 1001’s `standard` tier was absent, causing `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `default-missing-tier-discount`, using `TierDiscounts.GetValueOrDefault(customer.Tier)` so missing tiers receive a zero discount.
- **Test result:** `dotnet test` passed all 8 tests (0 failed). The localhost API remains on deployed 1.4.0; redeployment and live API verification were out of scope.

### Patch(es) applied
```diff
default-missing-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c304cd9 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (26 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E29

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier-discount table; order 1001’s `standard` tier was absent, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `default-missing-tier-discount`, changing the lookup to `TierDiscounts.GetValueOrDefault(customer.Tier)` so missing tiers receive no discount.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The running API is still on 1.4.0, so the fix will not affect its response until redeployment.

### Patch(es) applied
```diff
default-missing-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c304cd9 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (31 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git show --format= 9bfbf7d -- src/Orders.Api/Pricing.cs tests/Orders.Tests/PricingTests.cs: --format needs a valid value.
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E30

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build used `TierDiscounts[customer.Tier]`, which could throw `KeyNotFoundException` for the `standard` tier and cause `GET /orders/{id}/total` to return 500.
- Introduced by commit: `9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.
- Fix: changed the lookup to `TierDiscounts.GetValueOrDefault(customer.Tier)` in `src/Orders.Api/Pricing.cs`, restoring the old safe default-to-zero behavior for unknown tiers.
- Test result: `dotnet test` passed after the fix, with `8` tests passed and `0` failed.

### Patch(es) applied
```diff
fix-pricing-tier-default.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..c304cd9 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.GetValueOrDefault(customer.Tier);
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (28 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-pricing-tier-default, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = TierDi
🔐 approval needed: apply_proposal(name: fix-pricing-tier-default)
   ✔ approved
  ✔ applied fix-pricing-tier-default.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```
