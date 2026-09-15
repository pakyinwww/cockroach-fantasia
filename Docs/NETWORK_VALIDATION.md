# Four-player network validation

## Configuration

- Transport: Unity Transport over Sessions/Relay, listen-server host, no dedicated gameplay server.
- NGO tick rate: 30 Hz; client connection buffer timeout: 20 seconds.
- Player transforms: owner authoritative, interpolated, unreliable deltas, half-float positions,
  Y rotation only, and no scale synchronization.
- Test harness: one host plus three standalone Windows development players, isolated player profiles,
  real anonymous Authentication/Sessions/Relay, and Multiplayer Tools Network Simulator impairment.
- `OneWayDelayMs` is applied to every peer; 40 ms approximates 80 ms RTT and 75 ms approximates
  150 ms RTT. Loss is applied independently by the simulator.

## Repeatable commands

```powershell
.\Tools\Run-MovementRelayDiagnostic.ps1 -OutputName baseline -ResultsRematch
.\Tools\Run-MovementRelayDiagnostic.ps1 -OutputName rtt80-host-roach -OneWayDelayMs 40 -ResultsRematch -HostAsCockroach
.\Tools\Run-MovementRelayDiagnostic.ps1 -OutputName rtt150-loss2 -OneWayDelayMs 75 -PacketLossPercent 2 -ResultsRematch
.\Tools\Run-MovementRelayDiagnostic.ps1 -OutputName rtt150-loss2-host-roach -OneWayDelayMs 75 -PacketLossPercent 2 -ResultsRematch -HostAsCockroach -BriefInterruption
```

Each run validates four unique seats, movement observation and correction limits, one shared match
deadline, synchronized results, three match cycles, clean rematch state, one game manager, one enabled
listener, and a clean final Session leave. Movement output includes the Unity Profiler's sampled
`Total Bytes Sent` counter. Preserve `Builds/Diagnostics/Movement/<name>/*.log` as release evidence.
The brief-interruption switch injects a recoverable 650 ms lag spike with 80 ms jitter, then restores
the requested steady-state profile and waits for all peers to settle before continuing.

## Listen-server and environment limitations

The host has zero network transit latency to its own authoritative state and receives a latency
advantage; host migration is outside MVP scope, so a host departure ends the room with a clear error.
Relay availability and regional routing remain Unity service dependencies. Network simulation is a
repeatable impairment model, not a substitute for the final two-physical-network manual gate. That
gate needs four human-operated Windows PCs (including minimum and recommended hardware) and cannot
be honestly certified by four automated processes on one workstation.

## 2026-09-15 automated evidence

| Profile | Host role | Four peers / three cycles | Sampled sent bytes per peer |
| --- | --- | --- | --- |
| Baseline | Human | Passed | 1,112–12,696 |
| Approx. 80 ms RTT | Cockroach | Passed | 1,168–13,096 |
| Approx. 150 ms RTT + 2% loss | Human | Passed | 1,112–13,416 |
| Approx. 150 ms RTT + 2% loss | Cockroach | Passed after result-ack fix | 1,120–13,216 |
| Same plus recoverable lag spike | Cockroach | Passed after result-ack fix | 848–11,616 |

The Cockroach-host loss run initially showed that the host could start a rematch before one peer had
presented Results. The release fix adds an explicit per-client Results acknowledgement and gates both
host result buttons and server transitions until every connected client has acknowledged. Subsequent
runs completed all three cycles with identical winners/scores, no stale roster entries, no duplicated
managers, and clean Session leave. The sampled counter is a comparative three-second diagnostic value,
not a full-match byte total.
