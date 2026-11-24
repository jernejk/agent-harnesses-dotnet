I couldn’t independently check `status.example.com` with the available tools.

- **Root cause:** `Pricing.cs` directly indexed the tier-discount table, which lacked the `standard` tier, causing a `KeyNotFoundException` and HTTP 500.
- **Commit that introduced it:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `default-missing-tier-discount`, defaulting an unlisted tier’s discount to zero.
- **Test result:** **8 passed, 0 failed**.