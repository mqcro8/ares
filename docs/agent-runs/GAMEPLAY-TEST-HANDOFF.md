# Gameplay test handoff

Branch: `test/gameplay-campaign`
Base: `5c0e3916ccc965e5861d614e356154eac73c1481`

This branch contains gameplay work only. The phone-controller and Vercel relay
changes remain in their separate worktrees and are not included.

## Validation

- `dotnet build ares.sln`: passed.
- Godot import: passed.
- Godot harnesses: 16/16 passed.
- Headless main-scene startup: passed.
- Full keyboard campaign playthrough: still needs manual verification.

## Deferred: hosted phone relay

Return to the phone-controller issue after gameplay testing. The hosted
`https://ares-seven-fawn.vercel.app/api/relay` returned HTTP 500
(`FUNCTION_INVOCATION_FAILED`), and the deployed controller was stale during
the last check. Diagnose from sanitized Vercel function logs and confirm
`REDIS_URL` is configured. Do not put secret values in this file. Then deploy
and verify pairing, gameplay input, context changes, and physical motion on a
real phone. No relay fix or deployment is included in this gameplay branch.
