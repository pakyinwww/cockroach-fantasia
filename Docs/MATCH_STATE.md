# Authoritative Match State

`NetworkGameManager` is the host-owned source of truth for the Kitchen round. It
replicates four compact values through NGO `NetworkVariable`s:

- `Phase`: `Loading`, `Countdown`, `Playing`, or `Results`;
- `DepositedPoints`;
- `Winner`; and
- `PlayingEndTimestamp`, expressed in NGO server time.

Clients derive the visible timer locally with
`max(0, PlayingEndTimestamp - NetworkManager.ServerTime.Time)`. No countdown value is
sent per frame, so every peer uses the same synchronized clock and deadline.

## Default rules

`Data/MatchRules/DefaultMatchRules.asset` contains the MVP tuning values:

| Rule | Value |
| --- | ---: |
| Playing duration | 240 seconds |
| Cockroach food quota | 12 points |
| Pre-round countdown | 3 seconds |
| Cockroach respawn delay | 3 seconds |

The lobby scene load enters `Loading`. After a short scene-settle window the server
starts `Countdown`; its authoritative timestamp opens `Playing`, whose end timestamp is
exactly 240 seconds later. Only `Playing` accepts score-changing gameplay requests.

The state machine checks the food quota before timeout. Consequently, a deposit that
reaches 12 points on the deadline's server tick awards the Cockroaches the match. Once
`Results` is entered, later ticks and requests cannot alter the winner or score.

State transitions and deadline precedence are tested without Unity services in
`MatchStateMachineTests`. The Kitchen smoke test verifies the authored rules and
networked manager. A four-peer Windows Relay check can be run after a development build:

```powershell
& .\Tools\Run-MovementRelayDiagnostic.ps1 -OutputName match-state -MatchState -SkipMovement
```

The command requires all four clients to reach `Playing` and report one identical
authoritative deadline.
