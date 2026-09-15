# MVP performance targets and profiling

## Targets

| Tier | Windows PC | Resolution / quality | Target |
| --- | --- | --- | --- |
| Minimum test PC | 4-core CPU, 8 GB RAM, GTX 1050 / RX 560 class GPU | 1280x800, Low | Stable 45 FPS |
| Recommended test PC | 6-core CPU, 16 GB RAM, GTX 1660 / RX 590 class GPU | 1920x1080, Medium | Stable 60 FPS |

The standalone player targets 60 FPS. Medium is the recommended assumption: one shadow-casting key
light, 30 Hz NGO simulation, interpolated owner-authoritative transforms, no real-time GI/reflections,
and the single compact Kitchen. Low is the fallback for minimum hardware.

## Repeatable built-player probe

```powershell
.\Tools\Run-MovementRelayDiagnostic.ps1 -OutputName performance -Performance -ResultsRematch
```

This launches four standalone Windows players through real Sessions/Relay. Every peer samples 230
steady frames after warmup and reports average/max frame time, maximum `GC Allocated In Frame`, managed
bytes, enabled cameras, renderers, colliders, and active pooled audio voices. The harness fails above
25 ms average or more than three frames above 16 KiB allocation, while still reporting the absolute
maximum so one-time service warmup remains visible. Its movement phase also samples network bytes.
Three rematch cycles verify stable counts for managers and enabled listeners.

Batch `-nographics` results are CPU/memory/network regression evidence only. GPU frame time and target-
hardware FPS must be captured with a rendered development build and Unity Profiler on both hardware
tiers; this repository must not claim those physical measurements from a headless workstation run.

## Optimizations and bounded systems

- Character presentation caches its carrier component instead of looking it up each rendered frame.
- Procedural audio uses a persistent pool capped at ten voices with per-cue rate limiting.
- Comic VFX self-destruct and disable collision immediately; kitchen dressing adds no gameplay collider.
- Only the locally owned camera and listener are active; rematch diagnostics reject duplicate managers
  or listener growth.
- Food uses one server-owned lifecycle per item and despawns deposited objects.

## 2026-09-15 headless capture

Four Relay peers held a 16.67 ms average with 16.86–17.09 ms maximum sampled frame time. After
warmup, maximum per-frame allocation was 328–812 bytes and no sampled frame exceeded 16 KiB. Each
peer reported one enabled camera, 141 renderers, 36 colliders, and no idle audio voice. Across three
rematches, managed-memory change ranged from -16,384 to +12,288 bytes; the host changed by +4,096
bytes. Every peer retained one game manager and one enabled listener. Sampled movement traffic ranged
from 632 to 9,296 sent bytes over the comparative three-second window.
