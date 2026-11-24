I’ve verified the code path: the app already registers problem details, and the crash-risky helper is exactly the one in `Program.cs`. I can now write the fix plan without changing code.
## Root cause (file:line)

`src/Orders.Api/Program.cs:29-30`

Current code:

```csharp
static IResult NotFound(int id) =>
    Results.Problem($"Order {id} was not found.", statusCode: 404);
```

This is the regression introduced in `ab38e53` (“Return problem details for unknown orders”). The app is calling `Results.Problem(...)` from the `NotFound` helper, and the incident notes indicate that this path is failing and surfacing as HTTP 500 on `GET /orders/{id}/total`.

## Change (the exact lines before and after)

Replace the helper at the bottom of `src/Orders.Api/Program.cs`.

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

## Why this is the smallest safe fix

- It changes only the broken response path.
- It restores the pre-1.4.0 behavior for missing orders without affecting successful `/total` calculations.
- It avoids any dependency on problem-details rendering behavior in this incident path.
- The rest of the endpoint logic, logging, and DI setup remain untouched.

## Verification (what to run and what it must show)

Run:

1. `dotnet test`

Must show:
- all tests pass, including `OrdersApiTests.Order_totals_are_available(orderId: 1001)`
- no remaining failures related to `GET /orders/1001/total`

If you have an endpoint-level check available in your test suite or harness, also confirm:
- `GET /orders/1001/total` returns HTTP 404 for unknown orders
- `GET /orders/{known-id}/total` still returns HTTP 200

## Risks

- This reverts the richer error body for unknown orders; clients expecting problem-details payloads will now receive a plain 404.
- If another caller relied on the problem-details shape, that behavior will change.
- If the real underlying issue is broader than this helper, this fix may only address the failing incident path, not other potential problem-details writes.