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
- **Root cause:** `src/Orders.Api/Pricing.cs:16` in the failing 1.4.0 build directly indexed `TierDiscounts` for the `"standard"` tier, which was absent and caused a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unlisted-tier-discounts`; the lookup now uses a null-safe `TryGetValue` and defaults unlisted tiers to zero discount.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The live API was not used for verification because it still runs the deployed 1.4.0 build.

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

### Key events (44 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-for-unlisted-tier-discounts, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disco
🔐 approval needed: apply_proposal(name: fallback-for-unlisted-tier-discounts)
   ✔ approved
  ✔ applied fallback-for-unlisted-tier-discounts.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E02

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

## Item E03

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed `TierDiscounts` with the customer tier. The dictionary lacked `"standard"`, causing a `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `fallback-unknown-customer-tier-discount`; the null-safe lookup preserves configured discounts and defaults unlisted tiers to zero.
- **Test result:** All 8 tests passed (0 failed). The running 1.4.0 API was not redeployed, so its live behavior remains unchanged.

### Patch(es) applied
```diff
fallback-unknown-customer-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..1b89a67 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is { } tier && TierDiscounts.TryGetValue(tier, out var rate) ? rate : 0m;
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
→ write_proposal(name: fallback-unknown-customer-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var di
🔐 approval needed: apply_proposal(name: fallback-unknown-customer-tier-discount)
   ✔ approved
  ✔ applied fallback-unknown-customer-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 116 ms - Orders.Tes…
```

## Item E04

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` directly indexed the discount table using the customer tier; `standard` was not a key, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `default-unlisted-tier-discount`. `Pricing.Total` now uses `TryGetValue` and defaults unlisted or null tiers to zero discount, preserving the gold and silver discounts.
- **Test result:** **8 passed, 0 failed.** The plan expected 9 tests, but this single-file proposal did not include the planned bronze regression test. I did not check the live API because it still runs the 1.4.0 deployment.

### Patch(es) applied
```diff
default-unlisted-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..99b5f44 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,10 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null &&
+                       TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
+            ? tierDiscount
+            : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (65 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: default-unlisted-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: default-unlisted-tier-discount)
   ✔ approved
  ✔ applied default-unlisted-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 87 ms - Orders.Test…
```

## Item E05

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

## Item E06

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

## Item E07

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier-discount dictionary; order 1001’s `"standard"` tier was absent, causing a `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unmapped-customer-tiers`, using a null-safe lookup that defaults unmapped tiers to a 0% discount.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The local API remains on deployed build 1.4.0; verification used the tests, not the API.

### Patch(es) applied
```diff
fallback-for-unmapped-customer-tiers.patch:
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

### Key events (50 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1001)
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-for-unmapped-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disco
🔐 approval needed: apply_proposal(name: fallback-for-unmapped-customer-tiers)
   ✔ approved
  ✔ applied fallback-for-unmapped-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E08

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

## Item E09

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

## Item E10

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed `TierDiscounts` with the customer tier. Since `standard` wasn’t in the table, order 1001 caused a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `fallback-tier-discount`: use the matching tier discount when present, otherwise fall back to `0m`, including for null tiers. The diff matches the plan.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The local API still runs deployed 1.4.0, so it may continue returning 500 until redeployment.

### Patch(es) applied
```diff
fallback-tier-discount.patch:
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

### Key events (44 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = customer
🔐 approval needed: apply_proposal(name: fallback-tier-discount)
   ✔ approved
  ✔ applied fallback-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E11

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the discount table; order 1001’s customer has tier `standard`, which is absent from the table, causing `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unknown-tier-discounts`, restoring a zero-discount fallback for unknown or null tiers while preserving configured discounts.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The deployed localhost API was not used for verification.

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

### Key events (39 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-for-unknown-tier-discounts, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discou
🔐 approval needed: apply_proposal(name: fallback-for-unknown-tier-discounts)
   ✔ approved
  ✔ applied fallback-for-unknown-tier-discounts.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
```

## Item E12

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

## Item E13

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed the discount dictionary, which contains only gold and silver rates; a standard-tier customer caused a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied the proposal’s `TryGetValue` lookup with a zero-discount fallback for unknown or null tiers.
- **Test result:** `dotnet test` passed **8/8** tests, including the order-1001 API case. The planned unknown-tier and null-tier regression tests were not added by the proposal, so the expected 10-test verification remains incomplete.

### Patch(es) applied
```diff
fallback-unknown-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..5ff2bdc 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,10 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null &&
+                       TierDiscounts.TryGetValue(customer.Tier, out var configuredDiscount)
+            ? configuredDiscount
+            : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (67 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-unknown-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: fallback-unknown-tier-discount)
   ✔ approved
  ✔ applied fallback-unknown-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
```

## Item E14

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the tier discount table; `standard` was missing, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `default-missing-tier-discount`: use the table’s discount when present and default missing or null tiers to zero.
- **Test result:** `dotnet test` passed — **8 passed, 0 failed, 0 skipped**. The live API remains on 1.4.0 until redeployed.

### Patch(es) applied
```diff
default-missing-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..0e0f28f 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is not null ? TierDiscounts.GetValueOrDefault(customer.Tier) : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (45 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = c
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 114 ms - Orders.Tes…
```

## Item E15

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the discount table by customer tier. Order 1001’s customer has tier `standard`, which is absent from the table, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `fallback-unknown-tier-discount`, which uses a zero-discount fallback for unknown or null tiers while preserving configured gold and silver discounts.
- **Test result:** `dotnet test` passed: **8 passed, 0 failed, 0 skipped**. The local API remains on deployed 1.4.0, so its live behavior was not used to verify the patch.

### Patch(es) applied
```diff
fallback-unknown-tier-discount.patch:
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

### Key events (46 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-unknown-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: fallback-unknown-tier-discount)
   ✔ approved
  ✔ applied fallback-unknown-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 79 ms - Orders.Test…
```

## Item E16

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:7–11,16` omits `standard` from the discount table, then directly indexes it, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `default-missing-tier-discount`; `Pricing.Total` now defaults missing tier discounts to zero.
- **Test result:** Passed — 8 passed, 0 failed. The deployed live API was not used for verification.

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

### Key events (43 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1001)
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 79 ms - Orders.Test…
```

## Item E17

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed `TierDiscounts` by customer tier; `standard` was missing, causing `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-zero-for-unknown-tier-discounts`; missing or null tiers now receive a zero discount while gold and silver discounts are retained.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The live API was not used for verification; it remains on the deployed 1.4.0 build.

### Patch(es) applied
```diff
fallback-zero-for-unknown-tier-discounts.patch:
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
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-zero-for-unknown-tier-discounts, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var d
🔐 approval needed: apply_proposal(name: fallback-zero-for-unknown-tier-discounts)
   ✔ approved
  ✔ applied fallback-zero-for-unknown-tier-discounts.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
```

## Item E18

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed `TierDiscounts` with the customer tier. The table lacked `standard`, causing `KeyNotFoundException`.
- **Commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unlisted-customer-tier`; pricing now uses a null-safe lookup and gives unlisted or null tiers no discount.
- **Test result:** The proposal’s test run passed **8/8**. However, the diff shows the two planned fallback tests were not added, so the plan’s required **10-test** verification was not met.

### Patch(es) applied
```diff
fallback-for-unlisted-customer-tier.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..1ead2ce 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,7 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier != null && TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (59 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-for-unlisted-customer-tier, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discou
🔐 approval needed: apply_proposal(name: fallback-for-unlisted-customer-tier)
   ✔ approved
  ✔ applied fallback-for-unlisted-customer-tier.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E19

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the discount table; the missing `standard` key caused a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-unknown-customer-tiers`: use `TryGetValue` only for non-null tiers and fall back to a zero discount, preserving the gold and silver discounts.
- **Test result:** All 8 tests passed (0 failed). The running localhost API is still on 1.4.0; it was not redeployed.

### Patch(es) applied
```diff
fallback-unknown-customer-tiers.patch:
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

### Key events (52 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-unknown-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount =
🔐 approval needed: apply_proposal(name: fallback-unknown-customer-tiers)
   ✔ approved
  ✔ applied fallback-unknown-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E20

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the discount table. The table lacked the `standard` tier, so the lookup threw `KeyNotFoundException` and caused the HTTP 500.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `default-missing-tier-discount`; null or unlisted tiers now receive a 0% discount while gold and silver keep their configured discounts.
- **Test result:** `dotnet test` passed: **8 passed, 0 failed**. The localhost service remains on the deployed 1.4.0 build; redeployment and live API verification were out of scope.

### Patch(es) applied
```diff
default-missing-tier-discount.patch:
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

### Key events (49 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
  ✖ DENIED by policy: git show --format=fuller --no-ext-diff 9bfbf7d: git show does not allow --no-ext-diff.
→ write_proposal(name: default-missing-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = c
🔐 approval needed: apply_proposal(name: default-missing-tier-discount)
   ✔ approved
  ✔ applied default-missing-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E21

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

## Item E22

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed the discount dictionary for the `standard` tier, which was not present, causing a `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unknown-customer-tiers`; pricing now uses a null-safe lookup and falls back to a 0% discount for tiers absent from the table.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The local API remains on the deployed 1.4.0 build, so its live behavior was not re-verified; redeployment is out of scope.

### Patch(es) applied
```diff
fallback-for-unknown-customer-tiers.patch:
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

### Key events (52 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-for-unknown-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discou
🔐 approval needed: apply_proposal(name: fallback-for-unknown-customer-tiers)
   ✔ approved
  ✔ applied fallback-for-unknown-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
```

## Item E23

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

## Item E24

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

## Item E25

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

## Item E26

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:7–11,16` defined discounts only for gold and silver, then indexed the dictionary directly. A standard-tier customer caused a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `default-unknown-tier-discount-to-zero`: a null-safe `TryGetValue` lookup now defaults unlisted tiers to zero discount, preserving the gold and silver rates.
- **Test result:** `dotnet test` passed **8/8**. The standard, gold, silver, rounding, three order-total API cases, and unknown-order 404 tests pass. The plan expected 10 tests, but the explicit unlisted-tier and null-tier tests were not added, so those cases aren’t independently verified by the suite.

### Patch(es) applied
```diff
default-unknown-tier-discount-to-zero.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..664a913 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,10 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = customer.Tier is { } tier &&
+                       TierDiscounts.TryGetValue(tier, out var tierDiscount)
+            ? tierDiscount
+            : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (82 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: default-unknown-tier-discount-to-zero, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disc
🔐 approval needed: apply_proposal(name: default-unknown-tier-discount-to-zero)
   ✔ approved
  ✔ applied default-unknown-tier-discount-to-zero.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 88 ms - Orders.Test…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 78 ms - Orders.Test…
```

## Item E27

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` in the failing 1.4.0 build directly indexed the discount table with the customer tier; `standard` was absent, causing `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `fix-null-tier-discount`; unmapped or null tiers now receive zero discount. The proposal did **not** add the planned null-tier regression test.
- **Test result:** `dotnet test` passed **8, failed 0**. The plan called for 9 passing tests, including the null-tier test, so that verification target was not fully met.

### Patch(es) applied
```diff
fix-null-tier-discount.patch:
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

### Key events (67 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /health)
→ write_proposal(name: fix-null-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = customer
🔐 approval needed: apply_proposal(name: fix-null-tier-discount)
   ✔ approved
  ✔ applied fix-null-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E28

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` directly indexed the discount table. Since `standard` was absent, order 1001’s total request threw `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `default-unmapped-tier-discount`; null or unmapped tiers now receive a zero discount while mapped tiers retain their table discounts.
- **Test result:** `dotnet test` passed **8 tests, 0 failed**. The plan expected 10 tests, but its two proposed null-tier and unmapped-tier regression tests were not included in the applied change.

### Patch(es) applied
```diff
default-unmapped-tier-discount.patch:
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

### Key events (69 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: default-unmapped-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: default-unmapped-tier-discount)
   ✔ approved
  ✔ applied default-unmapped-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
```

## Item E29

### Final report
- **Root cause:** In the failing 1.4.0 build, `src/Orders.Api/Pricing.cs:16` indexed `TierDiscounts` with the customer tier; `standard` was absent, causing a `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unlisted-customer-tiers`; configured discounts are retained, while null or unlisted tiers get zero discount.
- **Test result:** Proposal test run passed **8/8**. The plan expected 10 cases, but the resulting diff changed only `Pricing.cs`; the planned test additions and stronger API total assertions were not present. I did not use the live API, which remains on 1.4.0.

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

### Key events (76 tool calls in total)
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

## Item E30

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed the tier-discount table for `"standard"`, which is absent, causing `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied the null-safe zero-discount fallback for null or unlisted tiers in `Pricing.cs`; the diff did not include the two planned edge-case tests.
- **Test result:** `dotnet test` passed **8/8** with exit code 0. The plan called for 10 passing cases, so that coverage target was not met; localhost was not used because it still runs deployed 1.4.0.

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

### Key events (70 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git show 9bfbf7d --format= --stat: --format needs a valid value.
  ✖ DENIED by policy: git show --format=fuller --no-ext-diff 9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0 -- src/Orders.Api/Pricing.cs: git show does not allow --no-ext-diff.
→ write_proposal(name: fallback-for-unlisted-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disco
🔐 approval needed: apply_proposal(name: fallback-for-unlisted-customer-tiers)
   ✔ approved
  ✔ applied fallback-for-unlisted-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 85 ms - Orders.Test…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 85 ms - Orders.Test…
```
