# Authoritative role roster

`NetworkRoster` is a persistent server-owned `NetworkObject`. Its `NetworkList`
replicates client ID, sanitized short display name, distinct role seat, ready
flag, and connection state. The four selectable seats are exactly one Human and
three individually addressable Cockroach positions.

Clients submit only their desired seat. The host derives the caller from NGO RPC
metadata and applies claims serially, so simultaneous requests for one seat have
one winner. A targeted response tells the losing player why the claim failed.
Changing seats clears ready; requesting the already-owned seat is idempotent.
Disconnect callbacks remove the entire stale entry and free its seat.

The Lobby scene contains four large role buttons and a session-local display-name
field. `RosterDiagnosticCommandLineRunner` verifies replication and invariants in
real multi-process Relay sessions without enabling production services during
ordinary test runs.
