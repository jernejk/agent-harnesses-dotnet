### Symptom
Order totals fail with HTTP 500. The test suite has 3 failures and 5 passes; the failing pricing tests throw `KeyNotFoundException` for key `standard`.

### Reproduction
- `dotnet test`: failed `PricingTests.Totals_round_to_cents`, `PricingTests.Standard_customers_pay_list_price_plus_gst`, and `OrdersApiTests.Order_totals_are_available(orderId: 1001)`.
- `GET /orders/1001/total`: returned 500.

### Change that caused it (commit and subject)
`9bfbf7d` — **Move tier discounts into a lookup table and add silver tier**. It changed the previous gold-only conditional to a dictionary lookup, but the new table does not include the existing `standard` tier.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16`: `TierDiscounts[customer.Tier]` indexes the dictionary directly. The dictionary at lines 7–11 contains only `gold` and `silver`, so pricing a standard customer throws `KeyNotFoundException`.

### Evidence
`src/Orders.Api/OrderStore.cs:8` assigns customer 2, who owns order 1001, the `standard` tier. The pricing tests also expect standard-tier pricing at `tests/Orders.Tests/PricingTests.cs:14` and `:26`.

### Confidence
High — the exception, missing lookup key, affected customer data, failing tests, and API 500 all point to the same cause.