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

## Item E02

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

## Item E03

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

## Item E04

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

## Item E05

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

## Item E06

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

## Item E07

### Final report
All done. Here's the summary:

- **Root cause:** `src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];` threw `KeyNotFoundException` because the 1.4.0 `TierDiscounts` table only contained `["gold"]` and `["silver"]`, omitting `"standard"` (and any other unknown tier). Any standard-tier order (e.g. order 1001 → customer 2) therefore returned HTTP 500.
- **Introducing commit:** `9bfbf7d` — "Move tier discounts into a lookup table and add silver tier" (part of the 1.4.0 release, tagged `f1812c8` "Release 1.4.0").
- **Fix:** One find-and-replace on line 16 of `src/Orders.Api/Pricing.cs`: `TierDiscounts[customer.Tier]` → `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`. This restores 1.3.0 behavior for every tier missing from the table (standard and unknown) at 0% discount, while leaving the intended 1.4.0 gold (0.05m) and silver (0.02m) entries untouched.
- **Test result after the fix:** `dotnet test` → **Passed: 8, Failed: 0, Skipped: 0, Total: 8** — including the 3 previously failing tests (`PricingTests.Totals_round_to_cents`, `PricingTests.Standard_customers_pay_list_price_plus_gst`, `OrdersApiTests.Order_totals_are_available(1001)`).

Note: the fix is applied in the repo only; the live localhost service is still the deployed 1.4.0 build and will keep returning 500 for `/orders/1001/total` until a redeploy (out of scope).

### Patch(es) applied
```diff
fix-tier-discount-lookup.patch:
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

### Key events (44 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, name: fix-tier-discount-lookup, rationale: Commit 9bfbf7d replaced the
🔐 approval needed: apply_proposal(name: fix-tier-discount-lookup)
   ✔ approved
  ✔ applied fix-tier-discount-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 72 ms - Orders.Test…
```

## Item E08

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

## Item E09

### Final report
The fix is applied and verified via tests. The live API still returns 500, which is expected — the deployed 1.4.0 build is untouched by a repo patch and requires a redeploy (out of scope).

- **Root cause** — `src/Orders.Api/Pricing.cs:16` in the 1.4.0 build: `var discount = TierDiscounts[customer.Tier];` threw `KeyNotFoundException` for any tier missing from the table (only `gold` and `silver` were present), so `standard`-tier customers (e.g. customer 2 / order 1001) 500'd.
- **Commit that introduced it** — `9bfbf7d` ("Move tier discounts into a lookup table and add silver tier"), part of the 1.4.0 release (tag v1.3.0 predates it).
- **Fix** — changed line 16 to `var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);`, restoring the 1.3.0 semantics (non-gold tiers pay full list price) while keeping the 1.4.0 lookup-table structure and gold/silver entries intact.
- **Test result** — `dotnet test` now passes 8/8 (previously 3 failed / 5 passed): `Standard_customers_pay_list_price_plus_gst`, `Totals_round_to_cents`, and `Order_totals_are_available(1001)` all pass, with gold/silver pricing and `Unknown_orders_return_404` remaining green.

### Patch(es) applied
```diff
fix-tier-discount-lookup.patch:
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

### Key events (39 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, name: fix-tier-discount-lookup, rationale: The 1.4.0 TierDiscounts tab
🔐 approval needed: apply_proposal(name: fix-tier-discount-lookup)
   ✔ approved
  ✔ applied fix-tier-discount-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500
```

## Item E10

### Final report
The verification is complete. The fix was applied via the proposal and the tests ran green.

**Root cause**: `src/Orders.Api/Pricing.cs:16` — `var discount = TierDiscounts[customer.Tier];` indexed a dictionary seeded with only `"gold"` (0.05m) and `"silver"` (0.02m), so standard-tier customers (order 1001 → customer 2) threw `KeyNotFoundException: 'standard'` → HTTP 500.

**Introducing commit**: `9bfbf7d` — "Move tier discounts into a lookup table and add silver tier" (the 1.4.0 release commit `f1812c8` only bumps the version string; `ab38e53` is unrelated). The old 1.3.0 ternary `customer.Tier == "gold" ? 0.05m : 0m` implicitly treated every non-gold tier as 0%.

**Fix**: `src/Orders.Api/Pricing.cs:16` — changed `TierDiscounts[customer.Tier]` to `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`, restoring 1.3.0 behavior (any unlisted tier gets 0% discount) while keeping the intended 1.4.0 gold (5%) and silver (2%) entries. No other files touched.

**Test result after fix**: `dotnet test` → **Passed: 8, Failed: 0, Total: 8** (up from 5 passed / 3 failed). The live 1.4.0 build on localhost still returns 500 for standard-tier orders since the fix has not been redeployed — verification was via tests as expected.

### Patch(es) applied
```diff
fix-tier-discount-default.patch:
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

### Key events (55 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git grep -n "record\|class" -- src/Orders.Api: No shell operators (; | & < > ` $), one command.
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, name: fix-tier-discount-default, rationale: The 1.4.0 dictionary index
🔐 approval needed: apply_proposal(name: fix-tier-discount-default)
   ✔ approved
  ✔ applied fix-tier-discount-default.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 107 ms - Orders.Tes…
```

## Item E11

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

## Item E12

### Final report
The fix is applied and verified. Here's the summary:

- **Root cause** — `src/Orders.Api/Pricing.cs:16` in the failing 1.4.0 build: `var discount = TierDiscounts[customer.Tier];` threw `KeyNotFoundException` for any tier missing from the table (`"standard"`, plus any other unlisted tier), producing HTTP 500 on `GET /orders/{id}/total`. The 1.4.0 lookup table (only `"gold"` = 0.05m and `"silver"` = 0.02m) silently dropped the 1.3.0 default of 0% discount for non-gold tiers.
- **Commit that introduced it** — `9bfbf7d` ("Move tier discounts into a lookup table and add silver tier", Priya Nair, 2026-11-13), part of the 1.4.0 release (`f1812c8`).
- **Fix** — One-line change in `src/Orders.Api/Pricing.cs:16`: replaced the throwing indexer with `var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);`, restoring 1.3.0's "non-gold → 0%" behavior for every missing tier while leaving gold (0.05m) and silver (0.02m) unchanged.
- **Test result after the fix** — `dotnet test` (via apply_proposal): **8/8 passed, 0 failed**, including the three previously failing tests (`Totals_round_to_cents`, `Standard_customers_pay_list_price_plus_gst`, `Order_totals_are_available(1001)`). The live localhost API still returns 500 because the deployed 1.4.0 build hasn't been redeployed (out of scope); verification was via tests as expected.

### Patch(es) applied
```diff
fix-tier-discount-lookup.patch:
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

### Key events (41 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, name: fix-tier-discount-lookup, rationale: Root cause: the 1.4.0 looku
🔐 approval needed: apply_proposal(name: fix-tier-discount-lookup)
   ✔ approved
  ✔ applied fix-tier-discount-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
```

## Item E13

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

## Item E14

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

## Item E15

### Final report
Verification complete. The fix is applied and all tests pass; the live API still 500s as expected since the deployed 1.4.0 build requires a redeploy (out of scope).

- **Root cause** — `src/Orders.Api/Pricing.cs:16`: `var discount = TierDiscounts[customer.Tier];` indexes a dictionary (lines 7–11) that only contains `gold` and `silver`, so any `standard`-tier customer (order 1001 / customer 2, and the standard-tier test fixtures) throws `KeyNotFoundException`, surfaced as HTTP 500.
- **Commit** — `9bfbf7d` ("Move tier discounts into a lookup table and add silver tier"), which rewrote the discount logic and flipped customer 3 to `silver`, leaving `standard` unmapped.
- **Fix** — applied proposal `fix-tier-discount-missing-key`: line 16 changed to `var discount = TierDiscounts.GetValueOrDefault(customer.Tier, 0m);`, restoring the 1.3.0 behaviour (standard/unknown tiers → 0%) while preserving the 1.4.0 gold (0.05) and silver (0.02) rates.
- **Test result** — `dotnet test` after the fix: **8 passed, 0 failed** (previously 3 failed / 5 passed). The live `GET /orders/1001/total` still returns 500 until a redeploy, which is out of scope.

### Patch(es) applied
```diff
fix-tier-discount-missing-key.patch:
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

### Key events (39 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, name: fix-tier-discount-missing-key, rationale: The TierDiscounts dict
🔐 approval needed: apply_proposal(name: fix-tier-discount-missing-key)
   ✔ approved
  ✔ applied fix-tier-discount-missing-key.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500
```
