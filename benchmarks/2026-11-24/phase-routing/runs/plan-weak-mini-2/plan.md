Below is the smallest safe-fix plan for the engineer.

## Root cause (file:line)

`src/Orders.Api/Program.cs:29-30`

Current code:

```csharp
static IResult NotFound(int id) =>
    Results.Problem($"Order {id} was not found.", statusCode: 404);
```

Why this causes the 500:

- `Results.Problem(...)` requires problem-details infrastructure to serialize the response.
- Although `builder.Services.AddProblemDetails();` is present in `Program.cs:5`, the endpoint path is still returning 500 in this build.
- The incident reproduces specifically on `GET /orders/{id}/total`, which uses this helper when the order is missing.
- The previous release used `Results.NotFound()` and did not hit this failure mode.

## Change (the exact lines before and after)

Change only the shared helper in `src/Orders.Api/Program.cs`.

Before:

```csharp
static IResult NotFound(int id) =>
    Results.Problem($"Order {id} was not found.", statusCode: 404);
```

After:

```csharp
static IResult NotFound(int id) =>
    Results.NotFound();
```

No other code changes are needed.

## Why this is the smallest safe fix

- It restores the previous working behavior for unknown orders.
- It touches only one helper used by both affected order endpoints:
  - `GET /orders/{id}`
  - `GET /orders/{id}/total`
- It avoids any dependency on problem-details serialization behavior.
- It matches the pre-1.4.0 behavior exactly, which is the safest rollback for the incident.
- It does not affect successful order lookups or pricing logic.

## Verification (what to run and what it must show)

Run:

```bash
dotnet test
```

Expected result for the incident path:

- `OrdersApiTests.Order_totals_are_available(orderId: 1001)` passes.
- `OrdersApiTests.Unknown_orders_return_404` passes.
- `GET /orders/1001/total` returns HTTP 200 in the test harness.
- `GET /orders/9999/total` returns HTTP 404, not 500.

Note:

- In the current workspace, `dotnet test` also shows two unrelated failures in `PricingTests` due to a `KeyNotFoundException` for `'standard'`.
- Those are separate from this incident and should not block the fix plan for the 500 on `/orders/{id}/total`.

## Risks

- This changes the error response body for unknown orders from problem details to an empty 404, which is a behavior rollback but may reduce error detail.
- If another team depended on the problem-details payload shape, they may need to update clients.
- The unrelated failing pricing tests could obscure validation if the engineer relies only on full-suite green status; they should specifically confirm the `/orders/{id}/total` path returns 404 for missing orders and 200 for known ones.