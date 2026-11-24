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
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed `TierDiscounts` with the customer’s tier; `"standard"` was missing, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `add-standard-tier-discount`, adding a 0% discount for the standard tier.
- **Test result:** **8 passed, 0 failed**, matching the verification plan. The live API was not checked; it remains on the deployed 1.4.0 build until redeployment.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (46 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
```

## Item E02

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` looked up the customer tier in a dictionary without a `standard` entry, causing `KeyNotFoundException` for standard-tier orders.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `add-standard-tier-discount`, adding `standard` with a zero discount.
- **Test result:** **8 passed, 0 failed, 0 skipped**. The live API was not used for verification; it remains on 1.4.0 until redeployed.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (43 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 81 ms - Orders.Test…
```

## Item E03

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` looked up the customer tier in a table without a `standard` entry, causing a `KeyNotFoundException` for order 1001.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `add-standard-tier-discount`, adding `["standard"] = 0m`; the diff matches the plan.
- **Test result:** All 8 tests passed (0 failed, 0 skipped). The live API was not used for verification; it remains on deployed 1.4.0 until redeployed.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (41 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E04

### Final report
- Root cause: `src/Orders.Api/Program.cs:23` dereferenced `store.FindCustomer(order.CustomerId)!` in `GET /orders/{id}/total`; if the customer was missing, the handler threw and returned HTTP 500.
- Commit that introduced it: `ab38e53` (`Return problem details for unknown orders`) changed the endpoint to use a custom `NotFound` path, and `9bfbf7d` (`Move tier discounts into a lookup table and add silver tier`) introduced a second regression by making `Pricing.Total` throw for `standard` customers at `src/Orders.Api/Pricing.cs:16`.
- Fix: added a missing-customer guard in `Program.cs` and changed pricing to `TierDiscounts.GetValueOrDefault(customer.Tier)` so standard/unknown tiers default to zero discount.
- Test result after fix: all tests pass, `8 passed, 0 failed`.

### Patch(es) applied
```diff
default-standard-tier-discount.patch:
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


guard-missing-customer-in-total.patch:
diff --git a/src/Orders.Api/Program.cs b/src/Orders.Api/Program.cs
index 901101b..26fdb70 100644
--- a/src/Orders.Api/Program.cs
+++ b/src/Orders.Api/Program.cs
@@ -20,7 +20,12 @@ app.MapGet("/orders/{id:int}/total", (int id, OrderStore store, ILogger<Program>
         log.LogWarning("Order {OrderId} not found", id);
         return NotFound(id);
     }
-    var customer = store.FindCustomer(order.CustomerId)!;
+    var customer = store.FindCustomer(order.CustomerId);
+    if (customer is null)
+    {
+        log.LogWarning("Customer {CustomerId} for order {OrderId} not found", order.CustomerId, id);
+        return Results.Problem($"Customer {order.CustomerId} for order {id} was not found.", statusCode: 404);
+    }
     return Results.Ok(new OrderTotal(id, Pricing.Total(order, customer), "AUD"));
 });
 

```

### Key events (21 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: guard-missing-customer-in-total, file: src/Orders.Api/Program.cs, find:     var customer = store.FindCustomer(order.CustomerId)!;     return R…, replace:    
🔐 approval needed: apply_proposal(name: guard-missing-customer-in-total)
   ✔ approved
  ✔ applied guard-missing-customer-in-total.patch, running the tests…
→ write_proposal(name: default-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: default-standard-tier-discount)
   ✔ approved
  ✔ applied default-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 80 ms - Orders.Test…
```

## Item E05

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:17` indexed `TierDiscounts` by customer tier, but the table lacked `"standard"`, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Added `["standard"] = 0m` to `TierDiscounts` in `src/Orders.Api/Pricing.cs:11`, preserving the existing gold and silver discounts.
- **Test result:** All 8 tests passed (0 failed), including the standard-tier pricing and order 1001 endpoint tests. Live API verification was not used, as the deployed service has not been redeployed.

### Patch(es) applied
```diff
add-standard-tier-pricing.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (46 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
  ✖ DENIED by policy: git show --format=fuller --find-renames 9bfbf7d -- src/Orders.Api/Pricing.cs src/Orders.Api/OrderStore.cs tests/Orders.Tests/PricingTests.cs: git show does no
→ write_proposal(name: add-standard-tier-pricing, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stati
🔐 approval needed: apply_proposal(name: add-standard-tier-pricing)
   ✔ approved
  ✔ applied add-standard-tier-pricing.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 80 ms - Orders.Test…
```

## Item E06

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` used `TierDiscounts[customer.Tier]`, so a `"standard"` customer triggered `KeyNotFoundException` and HTTP 500.
- Commit that introduced it: `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed `Pricing.Total` to use `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)` so unknown/non-tiered customers fall back to no discount instead of throwing.
- Test result after the fix: `dotnet test` passed, `Orders.Tests.dll` with `8/8` tests passing.

### Patch(es) applied
```diff
fix-orders-total-standard-tier.patch:
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

### Key events (27 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1003/total)
→ http_get(path: /orders/1002/total)
→ write_proposal(name: fix-orders-total-standard-tier, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: fix-orders-total-standard-tier)
   ✔ approved
  ✔ applied fix-orders-total-standard-tier.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 81 ms - Orders.Test…
```

## Item E07

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` directly looks up the customer tier, but the table at lines 7–11 omitted `standard`, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Added `["standard"] = 0m` to the discount table; the direct lookup and other tier rates remain unchanged.
- **Test result:** **8 passed, 0 failed**. The local API remains on deployed 1.4.0; redeploy and live API verification were out of scope.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (49 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ http_get(path: /orders/1001)
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 79 ms - Orders.Test…
```

## Item E08

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` used `TierDiscounts[customer.Tier]`, which threw `KeyNotFoundException` for the existing `"standard"` tier.
- Commit that introduced it: `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` (`Move tier discounts into a lookup table and add silver tier`).
- Fix: `src/Orders.Api/Pricing.cs` now defaults unknown tiers to `0m` discount with `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`.
- Test result: `dotnet test` passed after the fix — `8 passed, 0 failed`; the `OrdersApiTests.Order_totals_are_available` and pricing tests are green.

### Patch(es) applied
```diff
fix-unknown-tier-total.patch:
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

### Key events (27 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-unknown-tier-total, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = TierDisc
🔐 approval needed: apply_proposal(name: fix-unknown-tier-total)
   ✔ approved
  ✔ applied fix-unknown-tier-total.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 79 ms - Orders.Test…
```

## Item E09

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:17` looks up the customer tier in `TierDiscounts`; the dictionary lacked `"standard"`, causing a `KeyNotFoundException` for order 1001.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Added `["standard"] = 0m` to `TierDiscounts` in `src/Orders.Api/Pricing.cs`, preserving zero-discount pricing for standard customers.
- **Test result:** The proposal’s test run passed all 8 tests (0 failed). The localhost API was not used for verification because it still runs the deployed 1.4.0 build.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..575e887 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -7,6 +7,7 @@ public static class Pricing
     static readonly Dictionary<string, decimal> TierDiscounts = new()
     {
         ["gold"] = 0.05m,
+        ["standard"] = 0m,
         ["silver"] = 0.02m,
     };
 

```

### Key events (37 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:         ["gold"] = 0.05m,         ["silver"] = 0.02m,, replace:         ["gold"] = 0.05m, 
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
  ✖ DENIED by policy: git show --no-ext-diff --format=fuller --stat 9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0: git show does not allow --no-ext-diff.
```

## Item E10

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` looks up the customer tier in a dictionary that lacked `standard`, causing `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `add-standard-tier-discount`, adding an explicit zero discount for `standard`.
- **Test result:** All 8 tests passed (0 failed). The live API was not used for verification because it still runs the deployed 1.4.0 build.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (37 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
```

## Item E11

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` directly indexed `TierDiscounts`, which has no `standard` entry; order 1001’s customer is `standard`, causing the `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `handle-unlisted-customer-tiers`; unlisted tiers now receive a zero discount while configured gold and silver discounts remain unchanged.
- **Test result:** The proposal’s test run passed: **8 passed, 0 failed**. The deployed localhost API remains on 1.4.0 and was not used to verify the repository patch.

### Patch(es) applied
```diff
handle-unlisted-customer-tiers.patch:
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

### Key events (44 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: handle-unlisted-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = 
🔐 approval needed: apply_proposal(name: handle-unlisted-customer-tiers)
   ✔ approved
  ✔ applied handle-unlisted-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
  ✖ DENIED by policy: git show --oneline --no-ext-diff 9bfbf7d -- src/Orders.Api/Pricing.cs: git show does not allow --no-ext-diff.
```

## Item E12

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed the discount dictionary with customer tier `standard`, which is not a key, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `default-unknown-tier-discount`, using `GetValueOrDefault` so unlisted tiers receive no discount.
- **Test result:** All 8 tests passed (0 failed). The localhost API was not used for verification because it still runs the deployed 1.4.0 build.

### Patch(es) applied
```diff
default-unknown-tier-discount.patch:
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

### Key events (46 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: default-unknown-tier-discount, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = T
🔐 approval needed: apply_proposal(name: default-unknown-tier-discount)
   ✔ approved
  ✔ applied default-unknown-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 87 ms - Orders.Test…
```

## Item E13

### Final report
- root cause: `src/Orders.Api/Pricing.cs:16` used `TierDiscounts[customer.Tier]`, and the new lookup table added in 1.4.0 omitted the `standard` tier, causing `KeyNotFoundException` and HTTP 500 for normal orders.
- the commit that introduced it: `ab38e53` isn’t the culprit; the regression was introduced by `9bfbf7d Move tier discounts into a lookup table and add silver tier`.
- the fix: added `["standard"] = 0m` to the discount table in `src/Orders.Api/Pricing.cs` so the prior default behavior is restored.
- the test result after the fix: `dotnet test` passed, `8 passed, 0 failed`.

### Patch(es) applied
```diff
fix-standard-tier-pricing.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (11 tool calls in total)
```
→ http_get(path: /health)
→ write_proposal(name: fix-standard-tier-pricing, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stati
🔐 approval needed: apply_proposal(name: fix-standard-tier-pricing)
   ✔ approved
  ✔ applied fix-standard-tier-pricing.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 90 ms - Orders.Test…
```

## Item E14

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` directly indexes the discount table, which lacked an entry for `standard`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Added an explicit `standard` tier discount of `0m` in `Pricing.cs`.
- **Test result:** `dotnet test` passed: **8 passed, 0 failed**. The live 1.4.0 service was not redeployed.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (44 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
  ✖ DENIED by policy: git show --format=fuller --find-renames 9bfbf7d -- src/Orders.Api/Pricing.cs src/Orders.Api/OrderStore.cs tests/Orders.Tests/PricingTests.cs: git show does no
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 77 ms - Orders.Test…
```

## Item E15

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:17` directly indexes the discount table, which lacked `standard`; order 1001’s customer is standard tier.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Added `["standard"] = 0m` to the discount table.
- **Test result:** `dotnet test` completed successfully: **8 passed, 0 failed**. The live API was not used for verification; its deployed 1.4.0 build remains unchanged until redeployment.

### Patch(es) applied
```diff
add-standard-tier-zero-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (51 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1001)
  ✖ DENIED by policy: git show --no-ext-diff --format=medium 9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0 -- src/Orders.Api/Pricing.cs src/Orders.Api/OrderStore.cs tests/Orders.Tests/P
→ write_proposal(name: add-standard-tier-zero-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:    
🔐 approval needed: apply_proposal(name: add-standard-tier-zero-discount)
   ✔ approved
  ✔ applied add-standard-tier-zero-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
  ✖ DENIED by policy: git show --format=fuller --no-ext-diff 9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0 -- src/Orders.Api/Pricing.cs: git show does not allow --no-ext-diff.
```

## Item E16

### Final report
- root cause: `src/Orders.Api/Pricing.cs` line with `TierDiscounts[customer.Tier]` threw a `KeyNotFoundException` for `standard` customers, causing GET `/orders/{id}/total` to return HTTP 500.
- the commit that introduced it: `9bfbf7d` (“Move tier discounts into a lookup table and add silver tier”) in the 1.4.0 release range.
- the fix: changed the lookup to `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)` so standard/unknown tiers fall back to no discount instead of crashing.
- the test result after the fix: `Orders.Tests` passed, 8/8 tests green.

### Patch(es) applied
```diff
fix-pricing-tier-lookup.patch:
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

### Key events (15 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-pricing-tier-lookup, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = TierDis
🔐 approval needed: apply_proposal(name: fix-pricing-tier-lookup)
   ✔ approved
  ✔ applied fix-pricing-tier-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E17

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` before the fix (the lookup is now line 17) indexed the discount table with the customer’s tier; `standard` was missing, causing a `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `add-standard-tier-discount`, adding an explicit `standard` discount of `0m`.
- **Test result:** All 8 tests passed. The suite covers standard totals of `110.00` and `21.89`, and HTTP 200 responses for orders 1001–1003. The deployed API was not used for verification.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (51 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git show --format=fuller --find-renames 9bfbf7d: git show does not allow --find-renames.
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 79 ms - Orders.Test…
```

## Item E18

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed the discount table directly; `standard` is absent, causing an unhandled `KeyNotFoundException` for order 1001.
- **Introducing commit:** `9bfbf7d8` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-unknown-customer-tiers`; missing or null tiers now receive a zero discount. The proposal changed only `Pricing.cs` and did not add the planned regression tests.
- **Test result:** `dotnet test` passes **8/8** after the fix. That is below the plan’s expected 11 tests because the proposal omitted the test changes; I did not use the live API for verification.

### Patch(es) applied
```diff
fallback-unknown-customer-tiers.patch:
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

### Key events (52 tool calls in total)
```
→ http_get(path: /orders/1001/total)
→ http_get(path: /health)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git show --format=fuller --find-renames 9bfbf7d: git show does not allow --find-renames.
  ✖ DENIED by policy: git show --format=fuller --find-renames ab38e53: git show does not allow --find-renames.
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fallback-unknown-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount =
🔐 approval needed: apply_proposal(name: fallback-unknown-customer-tiers)
   ✔ approved
  ✔ applied fallback-unknown-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 80 ms - Orders.Test…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 80 ms - Orders.Test…
```

## Item E19

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` directly looks up `customer.Tier`, but `TierDiscounts` lacked `standard`; the lookup throws `KeyNotFoundException`. `src/Orders.Api/OrderStore.cs:8` assigns that tier.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `add-standard-tier-zero-discount`, adding `["standard"] = 0m` to the discount dictionary.
- **Test result:** **8 passed, 0 failed**. The deployed localhost API remains on 1.4.0; the fix will reach it only after redeployment.

### Patch(es) applied
```diff
add-standard-tier-zero-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (46 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1001)
→ http_get(path: /orders/1002/total)
→ write_proposal(name: add-standard-tier-zero-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:    
🔐 approval needed: apply_proposal(name: add-standard-tier-zero-discount)
   ✔ approved
  ✔ applied add-standard-tier-zero-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 79 ms - Orders.Test…
```

## Item E20

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` looked up the customer’s tier in a table without a `standard` entry, causing a `KeyNotFoundException` for order 1001.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `add-standard-tier-discount`, adding `["standard"] = 0m` to the tier discount table.
- **Test result:** Passed; **8 passed, 0 failed** (exit code 0). The local API remains on deployed 1.4.0 and was not used to verify the fix.

### Patch(es) applied
```diff
add-standard-tier-discount.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..d5985af 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -8,6 +8,7 @@ public static class Pricing
     {
         ["gold"] = 0.05m,
         ["silver"] = 0.02m,
+        ["standard"] = 0m,
     };
 
     public static decimal Total(Order order, Customer customer)

```

### Key events (45 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: add-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: add-standard-tier-discount)
   ✔ approved
  ✔ applied add-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 83 ms - Orders.Test…
```

## Item E21

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` used `TierDiscounts[customer.Tier]`, which threw `KeyNotFoundException` for existing `"standard"` customers and caused `GET /orders/{id}/total` to return HTTP 500.
- Commit that introduced it: `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed `src/Orders.Api/Pricing.cs` to use `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`, preserving gold/silver discounts and safely defaulting unknown tiers to no discount.
- Test result after the fix: `dotnet test` passed, with `8 passed, 0 failed`; the affected pricing and API tests are green.

### Patch(es) applied
```diff
fix-order-total-unknown-tier.patch:
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

### Key events (33 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fix-order-total-unknown-tier, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = Ti
🔐 approval needed: apply_proposal(name: fix-order-total-unknown-tier)
   ✔ approved
  ✔ applied fix-order-total-unknown-tier.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 75 ms - Orders.Test…
```

## Item E22

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed the tier-discount dictionary directly; `standard` was not a key, causing `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `restore-zero-discount-tier-fallback`; null and unrecognized tiers now receive a zero discount, while gold and silver retain their discounts.
- **Test result:** **8 passed, 0 failed.** The plan expected 10 cases, but the proposal diff changed only `Pricing.cs` and did not add the planned regression tests, so those additional cases were not verified.

### Patch(es) applied
```diff
restore-zero-discount-tier-fallback.patch:
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

### Key events (46 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: restore-zero-discount-tier-fallback, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discou
🔐 approval needed: apply_proposal(name: restore-zero-discount-tier-fallback)
   ✔ approved
  ✔ applied restore-zero-discount-tier-fallback.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
```

## Item E23

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` used `TierDiscounts[customer.Tier]`, but the dictionary only had `gold` and `silver`, so `standard` customers threw `KeyNotFoundException` and `/orders/{id}/total` returned HTTP 500.
- Commit that introduced it: `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: changed the lookup to `TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m`, preserving discounts for known tiers and defaulting unknown tiers to no discount.
- Test result after the fix: `dotnet test` passed with `0 failed, 8 passed`; the previously failing pricing and API tests are now green.

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

### Key events (31 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git show --stat --oneline --summary ab38e53: git show does not allow --summary.
  ✖ DENIED by policy: git show --stat --oneline --summary 9bfbf7d: git show does not allow --summary.
→ write_proposal(name: fix-orders-total-tier-lookup, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier]; , replace:         var discount = T
🔐 approval needed: apply_proposal(name: fix-orders-total-tier-lookup)
   ✔ approved
  ✔ applied fix-orders-total-tier-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 78 ms - Orders.Test…
```

## Item E24

### Final report
- Root cause: `src/Orders.Api/Pricing.cs:16` used `TierDiscounts[customer.Tier]`, so the `"standard"` tier for order `1001` threw `KeyNotFoundException` and became HTTP 500.
- Commit that introduced it: `9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`.
- Fix: applied `fix-standard-tier-total-lookup`, changing the lookup to `TryGetValue(...) ? tierDiscount : 0m` so unknown tiers no longer throw.
- Test result after the fix: `dotnet test` passed, `8 passed, 0 failed`.

### Patch(es) applied
```diff
fix-standard-tier-total-lookup.patch:
diff --git a/src/Orders.Api/Pricing.cs b/src/Orders.Api/Pricing.cs
index 5ee439e..0b2c009 100644
--- a/src/Orders.Api/Pricing.cs
+++ b/src/Orders.Api/Pricing.cs
@@ -13,7 +13,9 @@ public static class Pricing
     public static decimal Total(Order order, Customer customer)
     {
         var subtotal = order.Lines.Sum(line => line.Quantity * line.UnitPrice);
-        var discount = TierDiscounts[customer.Tier];
+        var discount = TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount)
+            ? tierDiscount
+            : 0m;
         var total = subtotal * (1 - discount) * (1 + GstRate);
         return Math.Round(total, 2, MidpointRounding.AwayFromZero);
     }

```

### Key events (32 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1002/total)
→ http_get(path: /orders/1003/total)
→ write_proposal(name: fix-standard-tier-total-lookup, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier]; , replace:         var discount =
🔐 approval needed: apply_proposal(name: fix-standard-tier-total-lookup)
   ✔ approved
  ✔ applied fix-standard-tier-total-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 78 ms - Orders.Test…
```

## Item E25

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` directly indexed the tier discount table; `standard` was absent, causing a `KeyNotFoundException` and HTTP 500.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Replaced the direct indexer with `TierDiscounts.GetValueOrDefault(customer.Tier, 0m)`, preserving listed discounts and giving unlisted tiers no discount.
- **Test result:** All 8 tests passed (0 failed, 0 skipped).

### Patch(es) applied
```diff
fallback-zero-for-unlisted-tier-discounts.patch:
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

### Key events (36 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-zero-for-unlisted-tier-discounts, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var 
🔐 approval needed: apply_proposal(name: fallback-zero-for-unlisted-tier-discounts)
   ✔ approved
  ✔ applied fallback-zero-for-unlisted-tier-discounts.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 87 ms - Orders.Test…
```

## Item E26

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` used `TierDiscounts[customer.Tier]`, which throws `KeyNotFoundException` for customers on tiers not present in the lookup table, causing `/orders/{id}/total` to return HTTP 500.
- **Commit that introduced it:** `9bfbf7d` — *“Move tier discounts into a lookup table and add silver tier”*.
- **Fix:** Changed the lookup to `TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m`, preserving the old behavior for unknown/standard tiers while still applying known discounts.
- **Test result after the fix:** `dotnet test` passed — **8 passed, 0 failed**.

### Patch(es) applied
```diff
fix-pricing-tier-lookup.patch:
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

### Key events (13 tool calls in total)
```
→ http_get(path: /health)
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ http_get(path: /orders/1001)
→ http_get(path: /health)
→ write_proposal(name: fix-pricing-tier-lookup, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount = TierDis
🔐 approval needed: apply_proposal(name: fix-pricing-tier-lookup)
   ✔ approved
  ✔ applied fix-pricing-tier-lookup.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 80 ms - Orders.Test…
```

## Item E27

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed a tier-discount map that lacked `"standard"`; order 1001 uses that tier (`src/Orders.Api/OrderStore.cs:8,14`), causing the total route to throw.
- **Introducing commit:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied a zero-discount fallback for null or unrecognized tiers, preserving the gold and silver discounts.
- **Test result:** **8 passed, 0 failed.** The plan expected 12 tests, but the proposal added only the production change; the specified regression tests were not present, so that verification target was not met.

### Patch(es) applied
```diff
fallback-for-unrecognized-order-tiers.patch:
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

### Key events (56 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
  ✖ DENIED by policy: git show --format= 9bfbf7d -- src/Orders.Api/Pricing.cs tests/Orders.Tests/PricingTests.cs src/Orders.Api/OrderStore.cs: --format needs a valid value.
→ write_proposal(name: fallback-for-unrecognized-order-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var disc
🔐 approval needed: apply_proposal(name: fallback-for-unrecognized-order-tiers)
   ✔ approved
  ✔ applied fallback-for-unrecognized-order-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 83 ms - Orders.Test…
```

## Item E28

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed the tier-discount dictionary directly, but it has no `standard` entry; pricing order 1001 therefore threw `KeyNotFoundException`.
- **Introducing commit:** `9bfbf7d8` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix:** Applied `fallback-for-unknown-customer-tiers`; unrecognized tiers now receive a zero discount.
- **Test result:** The proposal’s test run passed **8/8**. The plan called for **9** tests, including an unknown-tier regression test and stronger API total assertions; those test changes were not included in the applied diff.

### Patch(es) applied
```diff
fallback-for-unknown-customer-tiers.patch:
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
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
```

## Item E29

### Final report
- **Root cause:** `src/Orders.Api/Pricing.cs:16` indexed `TierDiscounts` directly; `standard` was absent, causing a `KeyNotFoundException` for order 1001.
- **Introducing commit:** `9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — “Move tier discounts into a lookup table and add silver tier.”
- **Fix applied:** Unknown or null customer tiers now receive a zero discount, preserving configured gold and silver discounts.
- **Test result:** `dotnet test Orders.slnx` passed: **8 passed, 0 failed**. Note: the applied diff changes only `Pricing.cs`; it does not include the additional regression tests listed in the plan.

### Patch(es) applied
```diff
fallback-unknown-customer-tiers.patch:
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

### Key events (50 tool calls in total)
```
→ http_get(path: /orders/1001/total)
  ← GET /orders/1001/total -> 500 
→ write_proposal(name: fallback-unknown-customer-tiers, file: src/Orders.Api/Pricing.cs, find:         var discount = TierDiscounts[customer.Tier];, replace:         var discount =
🔐 approval needed: apply_proposal(name: fallback-unknown-customer-tiers)
   ✔ approved
  ✔ applied fallback-unknown-customer-tiers.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 74 ms - Orders.Test…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 76 ms - Orders.Test…
```

## Item E30

### Final report
→ todos_complete(items: [{"id":1,"reason":"Ran dotnet test and reproduced the 500 via failing …)
- **Root cause:** `src/Orders.Api/Pricing.cs:16` uses `TierDiscounts[customer.Tier]`, which throws `KeyNotFoundException` for `"standard"` customers and surfaces as HTTP 500 on `GET /orders/{id}/total`.
- **Commit that introduced it:** `9bfbf7d` — **“Move tier discounts into a lookup table and add silver tier”**
- **Fix:** Switched to `TierDiscounts.TryGetValue(customer.Tier, out var tierDiscount) ? tierDiscount : 0m` so unknown/non-discounted tiers default to no discount.
- **Test result after the fix:** `dotnet test` passed — **8 passed, 0 failed**.

### Patch(es) applied
```diff
fix-standard-tier-discount.patch:
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

### Key events (13 tool calls in total)
```
→ http_get(path: /health)
  ✖ DENIED by policy: sed -n '1,120p' src/Orders.Api/Pricing.cs: 'sed' is not allow-listed.
→ write_proposal(name: fix-standard-tier-discount, file: src/Orders.Api/Pricing.cs, find:     static readonly Dictionary<string, decimal> TierDiscounts = new() …, replace:     stat
🔐 approval needed: apply_proposal(name: fix-standard-tier-discount)
   ✔ approved
  ✔ applied fix-standard-tier-discount.patch, running the tests…
  ← exit 0 · Passed!  - Failed:     0, Passed:     8, Skipped:     0, Total:     8, Duration: 83 ms - Orders.Test…
```
