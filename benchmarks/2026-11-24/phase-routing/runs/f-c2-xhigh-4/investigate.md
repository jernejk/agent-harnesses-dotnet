### Symptom
`GET /orders/1001/total` returns HTTP 500. The local API reproduced it.

### Reproduction
`dotnet test` failed 3 of 8 tests: two pricing tests and the API test for order `1001`. The pricing failures report `KeyNotFoundException` for key `standard`; the API test expected `OK` but received `InternalServerError`.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the table at lines 7–11 contains only `gold` and `silver`. The existing `standard` tier therefore throws `KeyNotFoundException`. Order `1001` uses customer `2`, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`). Before this change, standard customers received the zero-discount fallback.

### Evidence
The release diff replaces the fallback expression with the dictionary lookup and adds `silver` without adding `standard`. Test output’s stack trace points to `Pricing.cs:16`. No code was changed. I couldn’t access `https://status.example.com` from this environment.

### Confidence
High. The live endpoint reproduction, failing tests, source data, release diff, and blame attribution all agree.