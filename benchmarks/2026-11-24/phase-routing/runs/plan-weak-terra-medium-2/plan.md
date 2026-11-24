## Root cause (file:line)

`src/Orders.Api/Pricing.cs:7–16`: the discount table lacks an entry for `standard`, and its indexed lookup throws `KeyNotFoundException`. Order 1001 belongs to a standard customer (`OrderStore.cs:8,14`).

The regression came from `9bfbf7d`, not the cited problem-details commit. In v1.3.0, standard customers received a zero discount. Problem details are already registered at `Program.cs:5`.

## Change (the exact lines before and after)

In `src/Orders.Api/Pricing.cs`, replace:

```csharp
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
    };
```

with:

```csharp
    {
        ["gold"] = 0.05m,
        ["silver"] = 0.02m,
        ["standard"] = 0m,
    };
```

## Why this is the smallest safe fix

It restores the known standard-tier behavior without changing gold, silver, or unknown-order handling. Existing pricing tests already assert the standard total and rounding.

## Verification (what to run and what it must show)

- Run `dotnet test`: all 8 tests should pass. Before the fix, `dotnet test --no-restore` produced 3 failures and 5 passes.
- Run the API with `dotnet run --project src/Orders.Api`. Request `/orders/1001/total`: it should return HTTP 200 with total `195.80` and currency `AUD`.
- Request `/orders/1002/total` and `/orders/1003/total`: both should return HTTP 200. Request `/orders/9999/total`: it should still return HTTP 404.

## Risks

- Other unrecognized or differently cased tiers still throw; this targeted fix deliberately does not silently price them as standard.
- The status-page check was blocked by the tool policy, so an external incident could not be confirmed.
- The change has **not** been applied or verified; the working tree remains clean.