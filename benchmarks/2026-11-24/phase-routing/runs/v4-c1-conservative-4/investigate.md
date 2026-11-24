### Symptom
`GET /orders/1001/total` returns HTTP 500. The issue affects the standard-tier order; the other tier entries shown in the store have corresponding discount entries.

### Reproduction
- `GET /orders/1001/total` returned **500**.
- `dotnet test` failed **3 of 8** tests. The failures included two pricing tests and the API test for order 1001; each trace reported `KeyNotFoundException` for key `'standard'`.

### Change that caused it (commit and subject)
`9bfbf7d` — **“Move tier discounts into a lookup table and add silver tier”**. It replaced the non-gold fallback with a dictionary lookup, but added entries only for `gold` and `silver`.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`. For order 1001, `src/Orders.Api/OrderStore.cs:8,14` associates the order with a `standard` customer, but the dictionary at `Pricing.cs:7–11` has no `standard` entry. That lookup throws an unhandled `KeyNotFoundException`; the total route at `src/Orders.Api/Program.cs:23–24` then fails instead of returning a total. Before the change, non-gold tiers received a zero discount.

### Evidence
The API test expects HTTP 200 for order 1001 (`tests/Orders.Tests/OrdersApiTests.cs:10–16`). The v1.3.0 implementation used a gold-or-zero-discount fallback. The working tree is unchanged. **I can’t open the status page from this environment.**

### Confidence
High. The reproduced exception, affected customer data, route call, and change from v1.3.0 all point to the missing `standard` entry. I did not make a fix; this was an investigation only.