# Four-player Relay diagnostic

The issue #3 diagnostic uses the production Sessions flow, NGO, Unity Transport,
and Relay over DTLS. Each process must use a unique authentication profile.

Build the Windows development player, then start one host and three clients with
the following arguments (replace the profile suffix for each process):

```text
-batchmode -nographics -playerProfile relay-host -relayHostSmoke -roomCodeFile <shared-path> -relayDurationSeconds 600
-batchmode -nographics -playerProfile relay-client-1 -relayJoinSmoke -roomCodeFile <shared-path> -relayDurationSeconds 600
```

Start two additional clients with profiles `relay-client-2` and
`relay-client-3`. A successful process logs:

```text
RELAY_DIAGNOSTIC_FOUR_PLAYERS_CONNECTED
RELAY_DIAGNOSTIC_SUCCESS
```

The diagnostic continuously checks that all four NGO peers remain connected and
that exactly one uniquely owned player object exists per peer. Use a shared or
synchronized room-code file when testing across two physical Windows machines.
No dedicated gameplay server is involved; the host player owns the Relay-backed
NGO session.

Clients remain connected for a ten-second grace period after their successful
soak assertion. This lets the host complete its own assertion before peers close.

## Verification record

On 2026-09-13, one host and three standalone Windows development players used
four unique anonymous-authentication profiles to join Session code `H8JJG6`
through Relay DTLS. All four continuously validated four connections and four
uniquely owned player objects for 600 seconds, emitted no NGO warnings or
diagnostic failures, and exited with code 0. Logs remain local build artifacts
and are intentionally excluded from source control.

The `Relay external host smoke` workflow runs the complete diagnostic on four
independent GitHub-hosted Windows runners: one host and three clients. It
downloads the temporary `relay-smoke-build` prerelease asset, publishes the
ephemeral room code on the selected issue, soaks all four peers for ten minutes,
and retains one log artifact per runner. The host remains online for a short
grace period after its assertion so every client can complete cleanly.

A successful run comments `RELAY_EXTERNAL_FOUR_RUNNER_SUCCESS` on the selected
issue. This is external-machine Relay evidence, but it is not a substitute for
the issue's final hands-on test across two physical Windows PCs or its manual
playability observations.
