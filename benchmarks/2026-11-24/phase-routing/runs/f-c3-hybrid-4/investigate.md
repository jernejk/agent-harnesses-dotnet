### Symptom
`GET /orders/1001/total` returns HTTP 500. The failure affects the standard-tier order; gold order 1002 and silver order 1003 return 200. I can’t access `https://status.example.com` from this environment, so I couldn’t check for an incident.

### Reproduction
- `GET /orders/1001/total` → 500.
- `dotnet test` → 3 failed, 5 passed. Failures include two pricing tests and the API test for order 1001; the exception is `KeyNotFoundException` for key `standard`.

### Change that caused it (commit and subject)
`9bfbf7d` — **“Move tier discounts into a lookup table and add silver tier”**. This commit is included in v1.4.0, after v1.3.0.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:7–11` defines the tier-discount lookup with entries for `gold` and `silver`, but none for `standard`. At `src/Orders.Api/Pricing.cs:16`, pricing indexes that dictionary using the customer’s tier, which throws for standard customers.

Order 1001 belongs to a standard customer (`src/Orders.Api/OrderStore.cs:8,14`), and the total route calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`.

### Evidence
The v1.3.0-to-v1.4.0 diff shows the old fallback behavior—non-gold customers received no discount—was replaced by a direct dictionary lookup. The regression is also covered by the failing standard-tier pricing tests.

### Confidence
High. The failing API request, test exception, seeded customer tier, and changed lookup all point to the same cause. No files were modified.