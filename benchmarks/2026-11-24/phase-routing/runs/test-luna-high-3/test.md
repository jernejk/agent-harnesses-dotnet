I couldn’t independently check `status.example.com` with the available tools.

- **Root cause:** `Pricing.cs:16` indexed the tier-discount table directly; standard-tier customers were missing from the table, causing `KeyNotFoundException` and HTTP 500.
- **Commit that introduced it:** `9bfbf7d` — “Move tier discounts into a lookup table and add silver tier”.
- **Fix:** Applied `default-missing-tier-discount`, so unlisted tiers receive the default zero discount.
- **Test result after the fix:** **8 passed, 0 failed**.