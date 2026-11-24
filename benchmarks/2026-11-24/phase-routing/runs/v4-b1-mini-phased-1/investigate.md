## Symptom
`GET /orders/{id}/total` returns HTTP 500 after the 1.4.0 deploy.

## Reproduction
I reproduced it locally with:
- `GET /orders/1001/total -> 500`

## Change that caused it (commit and subject)
- `ab38e53` — **Return problem details for unknown orders**

This commit changed the not-found path in `src/Orders.Api/Program.cs` to use:
- `Results.Problem(..., statusCode: 404)`

## Root cause (file:line and why)
- `src/Orders.Api/Program.cs:30`

The helper:
```csharp
static IResult NotFound(int id) =>
    Results.Problem($"Order {id} was not found.", statusCode: 404);
```

is the bug. `Results.Problem` is meant to produce RFC7807 problem details, but in this code path it is being used in a way that triggers an exception/500 instead of a normal 404 for the `/orders/{id}/total` endpoint.

In other words, the regression is not in the lookup logic itself; it’s in the new error-response helper introduced in 1.4.0.

## Evidence
- Local API call returned `500` for `/orders/1001/total`
- `git log v1.3.0..HEAD` shows the regression was introduced after v1.3.0
- `git show ab38e53` shows the only functional change relevant to this path was replacing `Results.NotFound()` with `NotFound(id)`
- `git blame` points to line 30 in `Program.cs` as the new helper added in `ab38e53`
- `v1.3.0` used `Results.NotFound()` on the same code path and did not fail

## Confidence
High. The failing behavior reproduces locally, and the code change in `ab38e53` directly touches the error path for `/orders/{id}/total`.