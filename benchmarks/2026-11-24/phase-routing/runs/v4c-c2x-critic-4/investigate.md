### Symptom
`GET /orders/{id}/total` returns HTTP 500 for standard-tier customers.

### Reproduction
- `GET /orders/1001/total` returned **500**.
- `dotnet test` failed **3 of 8** tests; the pricing failures report `KeyNotFoundException` for key `standard`.
- `/orders/1001` returns customer ID `2`; `src/Orders.Api/OrderStore.cs:8` identifies that customer’s tier as `standard`.

### Change that caused it (commit and subject)
`9bfbf7d8` — **Move tier discounts into a lookup table and add silver tier**. It replaced the previous fallback behavior for non-gold tiers with a dictionary lookup.

### Root cause (file:line and why)
`src/Orders.Api/Pricing.cs:16` indexes `TierDiscounts` using `customer.Tier`, but the dictionary at lines 7–11 contains only `gold` and `silver`. For a `standard` customer, the lookup throws `KeyNotFoundException`, which causes the endpoint to return 500.

### Evidence
The test stack trace points to `Pricing.Total` at `Pricing.cs:16`; `git blame` attributes that line to `9bfbf7d8`. Before the change, non-gold tiers received no discount rather than causing an exception.

I can’t open `https://status.example.com` from this environment, so I couldn’t check for an incident. No fix was made during this investigation.

### Confidence
High.