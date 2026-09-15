# MVP performance targets and profiling

## Targets

| Tier | Windows PC | Resolution / quality | Target |
| --- | --- | --- | --- |
| Minimum test PC | 4-core CPU, 8 GB RAM, GTX 1050 / RX 560 class GPU | 1280x800, Low | Stable 45 FPS |
| Recommended test PC | 6-core CPU, 16 GB RAM, GTX 1660 / RX 590 class GPU | 1920x1080, Medium | Stable 60 FPS |

The standalone player targets 60 FPS and defaults to the Medium quality tier. Medium uses one shadow-casting key
light, 30 Hz NGO simulation, interpolated owner-authoritative transforms, no real-time GI/reflections,
and the single compact Kitchen. Low is the fallback for minimum hardware.

## Repeatable built-player probe

```powershell
.\Tools\Run-MovementRelayDiagnostic.ps1 -OutputName performance -Performance -ResultsRematch
.\Tools\Run-MovementRelayDiagnostic.ps1 -OutputName rendered-recommended -Performance -ResultsRematch -RenderedPrimary -RenderedWidth 1920 -RenderedHeight 1080
```

This launches four standalone Windows players through real Sessions/Relay. Every peer samples 230
steady frames after warmup and reports average/max frame time, maximum `GC Allocated In Frame`, managed
bytes, enabled cameras, renderers, colliders, and active pooled audio voices. The harness fails above
25 ms average or more than three frames above 16 KiB allocation, while still reporting the absolute
maximum so one-time service warmup remains visible. Its movement phase also samples network bytes.
Three rematch cycles verify stable counts for managers and enabled listeners.

The first command is CPU/memory/network regression evidence only. The second keeps the host rendered
while its three Relay peers run headless, so its frame-time sample is valid physical-PC evidence at the
requested resolution. Capture Unity Profiler GPU data alongside it for final sign-off. Both the minimum
and recommended hardware tiers still require their own rendered capture.

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

## 2026-09-15 rendered workstation capture

One physical Windows PC (Ryzen 5 5600X, RTX 5060, 1920x1080, Direct3D 12) rendered the Human host
while three headless peers shared the same live Relay match. The previously built player was still on
the stricter Ultra default. It held 16.67 ms average and 17.19 ms maximum frame time (60 FPS target),
completed three result/rematch cycles, retained one camera/listener, and showed 16 KiB managed-memory
growth. One warmup frame allocated 113,449 bytes; steady frames stayed within the harness budget.

That first capture exposed repeated NGO warnings from assigning avatar and food `NetworkVariable`s
before their `NetworkObject`s were spawned. Avatar seats are now assigned immediately after the player
spawn, and food initializes its authoritative state in server `OnNetworkSpawn`.

The rebuilt Medium-default player then passed 43/43 EditMode and 20/20 PlayMode tests. Its clean
four-peer rendered rerun held 16.67 ms average / 16.95 ms maximum frame time, allocated at most 344
bytes in sampled rendered frames, completed three rematches, retained one camera/listener, and showed
24,576 bytes host managed-memory growth. All peers exited 0 and the prior NGO warnings were absent.
Representative minimum-tier hardware is still required; the faster RTX 5060 workstation does not by
itself certify the documented GTX 1660/RX 590 tier.
