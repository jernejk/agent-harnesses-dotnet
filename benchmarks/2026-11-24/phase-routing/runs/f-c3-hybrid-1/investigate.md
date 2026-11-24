### Symptom
`GET /orders/1001/total` returns HTTP 500 in v1.4.0.

### Reproduction
The API call returned 500. `dotnet test` also failed: 3 tests failed and 5 passed. The failures include two pricing tests and the API test for order 1001; the exception is `KeyNotFoundException` for key `"standard"`.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This commit replaced the previous fallback pricing logic with a dictionary lookup, but added only `"gold"` and `"silver"` entries.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The map at lines 7–11 has no `"standard"` entry, so pricing order 1001 throws. `src/Orders.Api/OrderStore.cs:8` assigns that tier to its customer, and order 1001 uses that customer at line 14. The unhandled exception in the total route becomes HTTP 500.

### Evidence
The v1.3.0-to-v1.4.0 diff and `git blame` attribute the lookup change to `9bfbf7d`; the release tag v1.4.0 points to HEAD. The test output confirms the missing-key exception and failing API status. No files were changed.

I can’t open the status page because internet access is unavailable.

### Confidence
High. The runtime exception, affected order’s tier, and regression commit all agree.