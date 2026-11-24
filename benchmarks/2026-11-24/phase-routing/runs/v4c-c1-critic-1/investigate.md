### Symptom
`GET /orders/1001/total` returns HTTP 500.

### Reproduction
The local API returned 500. `dotnet test` reproduced the failure: 3 tests failed, including the endpoint test; the pricing tests reported `KeyNotFoundException` for key `standard`.

Status page: I couldn’t access it from this environment; there’s no internet access.

### Change that caused it (commit and subject)
`9bfbf7d8d7d0d9ec64a4a703468b3004dd9849c0` — **“Move tier discounts into a lookup table and add silver tier”**. The change replaced the previous fallback pricing behavior with a lookup containing only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` by `customer.Tier`. The dictionary at lines 7–11 has no `standard` entry, so pricing a standard-tier customer throws `KeyNotFoundException`. Order 1001 belongs to a standard-tier customer (`src/Orders.Api/OrderStore.cs:8,14`), and the total endpoint calls this pricing method (`src/Orders.Api/Program.cs:23–24`).

### Evidence
The exception occurs at `Pricing.Total` line 16, and the endpoint test expects `OK` but gets `InternalServerError`. The existing standard-tier pricing tests also fail. No files were modified.

### Confidence
High.