I have enough evidence to pinpoint the regression and will summarize it clearly for the engineer.
**Symptom**  
`dotnet test` fails in pricing and API total tests. Standard customers now throw `KeyNotFoundException: The given key 'standard' was not present in the dictionary`, and the API returns `500 InternalServerError` for order totals.

**Reproduction**  
Run `dotnet test`. The failures are:
- `Orders.Tests.PricingTests.Standard_customers_pay_list_price_plus_gst`
- `Orders.Tests.PricingTests.Totals_round_to_cents`
- `Orders.Tests.OrdersApiTests.Order_totals_are_available(orderId: 1001)`

**Change that caused it (commit and subject)**  
`9bfbf7d` — `Move tier discounts into a lookup table and add silver tier`

**Root cause (file:line and why)**  
`src/Orders.Api/Pricing.cs:16`  
The code changed from a defaulted conditional:
```csharp
var discount = customer.Tier == "gold" ? 0.05m : 0m;
```
to a direct dictionary lookup:
```csharp
var discount = TierDiscounts[customer.Tier];
```
But `TierDiscounts` only contains `"gold"` and `"silver"`, not `"standard"`. Standard customers therefore crash instead of getting 0% discount.

**Evidence**  
- Test output shows `KeyNotFoundException` for key `'standard'`.
- Stack trace points to `Orders.Api.Pricing.Total(...)` in `Pricing.cs:16`.
- `git show 9bfbf7d -- src/Orders.Api/Pricing.cs` shows the regression introduced in that commit.
- `git log v1.3.0..HEAD -- src/Orders.Api/Pricing.cs` shows only that one relevant change after the last good tag.

**Confidence**  
High. The failing key, the exact stack trace, and the code diff all align directly with the regression.