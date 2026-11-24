### Symptom
`GET /orders/1001/total` returns HTTP 500 on the local 1.4.0 API. I can’t access `https://status.example.com` from this environment to check for an incident.

### Reproduction
`GET /health` reports version 1.4.0. `GET /orders/1001/total` returns 500, while orders 1002 and 1003 return 200. `dotnet test` fails 3 of 8 tests; the failures include `KeyNotFoundException` for key `standard` and the API test for order 1001.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. The change replaced the prior fallback for non-gold customers with a direct dictionary lookup, but the new table includes only `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` throws when the customer’s tier has no dictionary entry. Order 1001 belongs to customer 2, whose tier is `standard` (`src/Orders.Api/OrderStore.cs:8,14`); `standard` is absent from the lookup (`Pricing.cs:7–11`). The total route calls this pricing method at `src/Orders.Api/Program.cs:24`, resulting in the 500.

### Evidence
The tests explicitly expect standard customers to be priced successfully (`tests/Orders.Tests/PricingTests.cs:13–14`), and the failing test stack trace points to `Pricing.cs:16`. The regression was introduced in the change since v1.3.0; the other recent commits don’t alter this pricing lookup. No files were changed.

### Confidence
High. The reproduced exception, order/customer data, route, and regression diff all point to the same missing `standard` mapping.