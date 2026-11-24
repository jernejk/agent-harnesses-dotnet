### Symptom
`GET /orders/1001/total` returns HTTP 500. Gold and silver orders succeed.

### Reproduction
`dotnet test`: 3 failed, 5 passed. The endpoint test fails because it expected 200 but got `InternalServerError`; the pricing tests throw `KeyNotFoundException` for the missing key `"standard"`. Direct API checks confirmed order 1001 returns 500, while orders 1002 and 1003 return 200.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit replaced the standard-tier fallback with a dictionary lookup but added only `"gold"` and `"silver"` entries.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 has no `"standard"` entry. Order 1001 is assigned to the standard-tier customer in `src/Orders.Api/OrderStore.cs:8` (the order-to-customer mapping is at line 14), so the lookup throws. The total route calls this calculation at `src/Orders.Api/Program.cs:24`, resulting in the 500.

### Evidence
Tests expect standard-tier pricing in `tests/Orders.Tests/PricingTests.cs:13–14` and `25–26`; both fail with the missing-key exception. The other post-v1.3.0 commit, `ab38e53` (“Return problem details for unknown orders”), changes not-found handling and is unrelated. No files were changed.

### Confidence
High: the failing stack trace, reproduced endpoint behavior, customer mapping, and post-v1.3.0 diff all point to the same cause.

Status page: I can’t access the external status page from this environment.