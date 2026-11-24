### Symptom
`GET /orders/{id}/total` returns HTTP 500 for orders whose customer tier is `standard`.

### Reproduction
- `GET /orders/1001/total` returned **500**.
- `dotnet test` failed **3 of 8** tests. The pricing failures and API test report a `KeyNotFoundException` for key `standard`.
- I could not open `https://status.example.com` from this environment.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. It replaced the previous “gold gets a discount, otherwise no discount” logic with a dictionary lookup, but only added `gold` and `silver`. The other post-v1.3.0 commit, `ab38e53` (“Return problem details for unknown orders”), concerns 404 responses for unknown orders.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` with `customer.Tier`. The dictionary at lines 7–11 has no `standard` entry, so this throws `KeyNotFoundException`. Order 1001 references customer 2 (`src/Orders.Api/OrderStore.cs:14`), whose tier is `standard` (`OrderStore.cs:8`); the total route calls `Pricing.Total` at `src/Orders.Api/Program.cs:24`.

### Evidence
At v1.3.0, `Pricing.Total` applied a 5% discount only to gold customers and otherwise used 0%—so `standard` worked. The 1.4.0 change introduced the missing-key lookup. The `standard` pricing tests still expect this tier to work (`tests/Orders.Tests/PricingTests.cs:13–14, 25–26`).

### Confidence
**High.** The reproduced response, test exception, changed code, and v1.3.0 baseline agree. No fix was applied.