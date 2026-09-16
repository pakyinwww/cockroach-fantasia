# Physical MVP sign-off

Use this checklist to close issues #3, #24, #25, and #26. Do not record public IP addresses,
authentication tokens, or other secrets. Non-sensitive labels such as `Home-A` and `Office-B` are
enough to distinguish external networks.

## Files under test

- Human playtest: download `CockroachFantasia-0.1.0-rc.1-Windows-Release.zip` from the
  `v0.1.0-rc.1` GitHub prerelease.
- Automated ten-minute connectivity: download `CockroachFantasia-Windows.zip` from the
  `relay-smoke-build` GitHub prerelease. It is a Development build from commit `386bc48` because
  command-line diagnostics are deliberately disabled in Release players. Its ZIP SHA-256 is
  `e3393bb75ae175536c0d4d8fb08067f79532d17e56985973193d07741ffc56f4`.
- Verify the Release ZIP SHA-256 is
  `81313f93c6b2eba65f8524faacec697553a41c5b3bd527ad657b98e7460ae1b8`.

## Two-machine Relay capture

Use at least two physical Windows PCs on different external networks. Open one PowerShell window for
each participant. Start the host first, copy its six-character room code, then start all three clients
within 120 seconds. `ParticipantId` values must be unique.

```powershell
# Physical PC / network A
.\Tools\Run-PhysicalRelayDiagnostic.ps1 -Role Host -ParticipantId Host -NetworkLabel Home-A -PlayerPath C:\CockroachFantasia-Development\CockroachFantasia.exe -Rendered

# Physical PC / network B (three separate PowerShell windows)
.\Tools\Run-PhysicalRelayDiagnostic.ps1 -Role Client -ParticipantId Client1 -NetworkLabel Office-B -RoomCode ABC123 -PlayerPath C:\CockroachFantasia-Development\CockroachFantasia.exe -Rendered
.\Tools\Run-PhysicalRelayDiagnostic.ps1 -Role Client -ParticipantId Client2 -NetworkLabel Office-B -RoomCode ABC123 -PlayerPath C:\CockroachFantasia-Development\CockroachFantasia.exe
.\Tools\Run-PhysicalRelayDiagnostic.ps1 -Role Client -ParticipantId Client3 -NetworkLabel Office-B -RoomCode ABC123 -PlayerPath C:\CockroachFantasia-Development\CockroachFantasia.exe
```

Each participant must finish with `PHYSICAL_RELAY_SUCCESS`. Preserve every generated `evidence.json`
and `player.log` under `Builds/Diagnostics/Physical`. The JSON records the executable hash, embedded
build manifest, CPU, GPU, RAM, OS, non-sensitive network label, room code, result markers, and exit code.

## Human-operated Release loop

Run the published Release build, not the diagnostic build. Four people must complete these checks:

- [ ] Host creates a private room and shares the displayed code.
- [ ] Three friends join by code; a fifth/full join and an invalid code show clear errors.
- [ ] Exactly one Human and three Cockroaches select distinct roles and ready up.
- [ ] Cockroaches collect, carry, drop, and deposit all food sizes; heavier food feels slower.
- [ ] Human swatter hits make Cockroaches drop food and respawn without gore or elimination.
- [ ] Timer, score, role HUD, results, and winner agree for all four players.
- [ ] Rematch completes with no stale roster, duplicated food, stuck respawn, or dual result.
- [ ] Voluntary client leave, host loss, and return to frontend are understandable.
- [ ] Complete at least one full match with the host as Human and one with the host as Cockroach.
- [ ] Record subjective movement/swatter feel and any visible correction on both networks.

## Hardware/FPS record

Capture a rendered Development build with Unity Profiler on both tiers. Use peak four-player action,
not an empty lobby, and attach a profiler capture or frame-time screenshot.

| Tier | Required setup | Average FPS | 1% low FPS | Max GC/frame | Result |
| --- | --- | ---: | ---: | ---: | --- |
| Minimum | 4-core, 8 GB, GTX 1050/RX 560 class; 1280x800 Low |  |  |  | Pending |
| Recommended | 6-core, 16 GB, GTX 1660/RX 590 class; 1920x1080 Medium |  |  |  | Pending |

Minimum passes at a stable 45 FPS; recommended passes at a stable 60 FPS. Note CPU, GPU, RAM, Windows
version, graphics driver, resolution, quality tier, host role, and whether the machine hosted or joined.

## GitHub evidence comment

Attach or link all eight generated files, the human checklist result, and both performance captures.
Include tester/date, non-sensitive network labels, build hash, room code, match/rematch outcomes, FPS
summary, warnings/errors observed, and accepted limitations. Only then close #3, #24, #25, and #26.
