### Symptom
`GET /orders/1001/total` returns HTTP 500. The API health endpoint reports version 1.4.0.

### Reproduction
`dotnet test` fails 3 of 8 tests. The failures include the API total test and two pricing tests; each throws `KeyNotFoundException` for the missing key `standard`. The local `GET /orders/1001/total` also returned 500.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. This change replaced the previous standard-tier fallback with a dictionary lookup, but the table includes only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws when the tier is `standard`, because that key is absent from the dictionary. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8`), and the total endpoint calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`; the exception surfaces as HTTP 500.

### Evidence
The test stack trace points to `Pricing.Total` line 16. Git history and blame attribute that lookup line and the table to `9bfbf7d`; the diff from v1.3.0 shows the earlier fallback was replaced. I could not open the external status page from this environment. No changes were made.

### Confidence
High.