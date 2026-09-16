# Windows MVP release candidate

## Build

The candidate is **0.1.0-rc.1**, Windows x86-64, Unity 6000.3.21f1. From a clean checkout with the
linked Unity Cloud project available:

```powershell
.\Tools\Build-Windows.ps1 -EditorPath 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe' -Configuration Development
.\Tools\Build-Windows.ps1 -EditorPath 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe' -Configuration Release -Archive
```

The Release command performs a clean build-cache build, writes `release-manifest.json`, and emits a
ZIP plus SHA-256 under `Builds/Artifacts`. Command-line smoke runners refuse to start when
`Debug.isDebugBuild` is false. Unity service configuration is supplied by the linked project, while
local service JSON and `.env` secrets are ignored by Git.

## Player troubleshooting

- Invalid/expired code: ask the host for the current six-character code and retry without spaces.
- Full or locked room: wait for a slot or for the current match to return to its lobby.
- Relay/service failure: check internet access, allow the executable through Windows Firewall, then retry.
- Host left: the room ends by design; return to FrontEnd and have a player create a new room.
- Poor feel: use wired networking where possible, lower resolution/quality, and let the lowest-latency
  player host. Reconnect/resume and host migration are outside MVP scope.

## Sign-off checklist

| Requirement | Evidence / status |
| --- | --- |
| Deterministic local rules and network contracts | 43 EditMode + 20 PlayMode tests pass without UGS |
| Real four-player create/join/roles/play/results/rematch | Standalone Relay diagnostics pass three cycles |
| 150 ms RTT + 2% loss, both host roles | Passed; per-client Results acknowledgements fixed a discovered race |
| Invalid/full/locked, leave/rejoin, client disconnect, host loss | Automated rule tests and opt-in lifecycle harness documented |
| No duplicate food, dual winner, stuck respawn, stale roster | Rule regressions and four-peer diagnostics pass |
| Audio/art/credits | Procedural/original sources documented; third-party notice included |
| Clean Release artifact, manifest, checksum | Produced by `Build-Windows.ps1`; record hash below |
| Two external physical networks | **Pending physical release gate** |
| Minimum/recommended rendered hardware FPS | **Pending physical release gate** |

Final production sign-off remains **conditional** until the two physical gates are run by human testers.
Do not relabel this candidate as final while either row is pending. Follow
`Docs/PHYSICAL_SIGNOFF.md` to capture the required evidence consistently.
