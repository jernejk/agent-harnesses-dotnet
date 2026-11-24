### Symptom
`GET /orders/1001/total` returns HTTP 500 on the local 1.4.0 service.

### Reproduction
- `GET /orders/1001/total` returned 500; `/health` returned 200 and reported version `1.4.0`.
- `dotnet test` failed 3 tests, including the API total test and two pricing tests, with `KeyNotFoundException: 'standard'`.
- I could not check `https://status.example.com`: this environment has no internet access.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the table at lines 7–11 contains only `"gold"` and `"silver"`, not `"standard"`. Order 1001 belongs to customer 2, whose tier is `"standard"` (`src/Orders.Api/OrderStore.cs:8,14`). The lookup throws, and `src/Orders.Api/Program.cs:24` calls it while handling the total endpoint.

### Evidence
- The v1.3.0 pricing code treated every non-gold tier as having no discount; the 1.4.0 change replaced that behavior with a direct dictionary index.
- The existing standard-tier pricing tests fail with the same missing-key exception.
- No files were changed; this was a diagnosis-only investigation.

### Confidence
High. The exception, failing tests, endpoint response, and v1.3.0-to-1.4.0 diff all point to the same cause.