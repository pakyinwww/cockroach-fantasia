# Cockroach Fantasia — MVP Design and Technical Implementation Plan

**Status:** Pre-production design baseline  
**Target:** Windows PC, Unity 6.3 LTS, keyboard and mouse  
**Players:** Exactly four online players: one Human and three Cockroaches  
**Networking:** Player-hosted listen server through Unity Relay; no dedicated gameplay server

## 1. MVP statement

Cockroach Fantasia is a cute, slapstick, asymmetric kitchen heist. Three tiny
Cockroaches have four minutes to steal enough food and return it to their nest.
One giant Human tries to delay them with a toy-like swatter. A swatted roach
drops its cargo, disappears in a cartoon puff, and returns at the nest a few
seconds later. Nobody is eliminated and there is no gore.

The MVP proves one complete, replayable online match:

1. A player creates a private room and shares its code.
2. Three friends join that room by code.
3. Players claim one Human seat and three Cockroach seats, then ready up.
4. The host starts a four-minute match on one kitchen map.
5. Cockroaches collect and deposit food while the Human swats them.
6. The server declares a winner and returns everyone to the same lobby for a rematch.

The target experience is readable, funny, and functional with temporary or
simple stylized assets. Content breadth is deliberately secondary to a robust
four-player loop.

## 2. Design pillars

- **Tiny thieves, enormous kitchen:** Everyday props create readable routes,
  hiding places, and visual comedy without extra abilities.
- **Funny setbacks, never punishment:** A swat costs carried food and a short
  amount of time, but never removes a player from the match.
- **Immediate teamwork:** Roaches share one score and can split routes without
  inventory management or character classes.
- **One-button clarity:** Picking up, dropping, and depositing use one interact
  input; the Human has one obvious swatter attack.
- **Short rematches:** A four-minute round plus a quick results screen makes
  balance iteration and repeated play easy.

## 3. Match rules

### 3.1 Win conditions

- The Cockroaches win immediately when the nest reaches **12 food points**.
- The Human wins when the server timer reaches **0:00** with fewer than 12
  deposited points.
- Deposited food is permanent and cannot be removed.
- The authoritative timer is exactly **240 seconds**. The lobby and results
  screens do not consume match time.

These numbers are starting balance values, stored in a `MatchRules` ScriptableObject
so they can be tuned without code changes.

### 3.2 Food

Each Cockroach can carry one item. Holding interact near the best valid target
picks it up; holding nothing and pressing interact drops it. Entering the nest
deposit volume while carrying deposits automatically.

| Size | Example prop | Points | Carry speed | Purpose |
|---|---|---:|---:|---|
| Small | Cracker crumb | 1 | 95% | Safe, nearby route |
| Medium | Cheese cube | 2 | 85% | Standard risk/reward |
| Large | Doughnut piece | 3 | 70% | Slow, exposed team objective |

The greybox map starts with 18 total food points at fixed, authored spawn
locations. Fixed locations make early tests repeatable. Randomized spawns are a
post-MVP option.

### 3.3 Swatter and respawn

- Primary mouse button starts a readable **0.25-second wind-up**.
- The active swat is a short server-validated swept volume in front of the Human,
  with an initial **1.8 m range** and **1.1-second total cooldown**.
- A valid hit makes the Cockroach drop its current item at the hit location.
- The Cockroach becomes non-interactive, plays a puff/squash reaction, and
  respawns at a free nest spawn point after **3 seconds**.
- The Human cannot affect food directly and cannot enter the nest's small safe
  interior. The entrance remains visible and swattable, preventing an absolute
  safe route.
- Multiple Cockroaches may be hit by one well-placed swat. This is funny and
  rewards timing, while the cooldown prevents constant attack spam.

All values are tuning defaults, not promises of final balance.

### 3.4 Movement and controls

Both roles use kinematic character movement. There is no jump, sprint, stamina,
climb, shove, or special ability in the MVP.

| Input | Cockroach | Human |
|---|---|---|
| WASD | Camera-relative movement | First-person movement |
| Mouse | Orbit close third-person camera | First-person look |
| E | Pick up / drop food | — |
| Left mouse | — | Swing swatter |
| Escape | Pause/options overlay | Pause/options overlay |

The pause overlay does not pause an online match. It exposes mouse sensitivity,
invert Y, master volume, and leave-room confirmation. Cockroach camera collision
prevents walls and props from blocking the view.

## 4. Single-map vertical slice

The MVP contains one compact kitchen with three recognizable lanes:

- **Counter lane:** Shortest and most exposed path, accessed by a skirting-board
  ramp and chair support.
- **Floor lane:** Open sight lines around table legs, food bowl, and dropped props.
- **Cabinet lane:** Longer route with cover beneath cabinets and appliances.

The nest sits inside a wall crack near one corner. Food is distributed so no
single Human position covers every route. Major collision uses simple primitive
colliders; small decorative clutter does not block players.

Art direction uses rounded forms, oversized food, warm kitchen colors, expressive
roach eyes/antennae, a foam-looking swatter, squash-and-stretch animation, and
comic particles. Audio focuses on tiny footsteps, food rustles, rubbery swats,
roach squeaks, score jingles, and a final countdown. All reactions remain playful
and bloodless.

## 5. Lobby and session flow

### 5.1 Front end

1. On launch, initialize Unity Services and sign in anonymously.
2. Show **Create Room**, **Join Room**, display-name input, settings, and quit.
3. Create a private four-player Session configured with Relay over DTLS. Display
   the Session code as the room code and offer a copy button.
4. Join by normalized code (trim whitespace and force uppercase). Present useful
   errors for invalid, full, expired, or unreachable rooms.
5. Once joined, NGO is connected through Relay and the player enters the networked
   Lobby scene.

No room browser or Matchmaker API is used.

### 5.2 Role room

- Four visible seats: one Human and three Cockroaches.
- Any player can claim a free seat. The listen-server host validates every claim,
  so simultaneous attempts cannot create two Humans.
- Changing seats clears that player's ready flag.
- Each player controls only their own ready toggle.
- Only the room host sees an enabled Start button, and only when there are exactly
  four connected players, every seat is valid, and all players are ready.
- Starting locks the roster. Late join attempts are rejected until the room
  returns to Lobby.
- After results, the host can select **Rematch** to return all connected players
  to the role room with ready flags cleared.

For MVP, if the listen-server host disconnects, every client receives a clear
“Host left” message and returns to the front end. Host migration and reconnection
are not included.

## 6. Player-facing UI

### Cockroach HUD

- Shared deposited score, shown as `8 / 12` with a nest icon.
- Server timer and an emphasized final-ten-second countdown.
- Carried item icon, point value, and speed penalty.
- Small center interaction prompt with target name.
- Respawn overlay and countdown after a swat.

### Human HUD

- Shared deposited score and server timer.
- Minimal center reticle.
- Swatter cooldown conveyed by animation and a small reticle fill, not an ability bar.

### Results

- Large role-specific win line and a one-sentence comic summary.
- Final deposited total.
- Host-controlled Rematch and Return to Menu; other players see waiting status.

There is no minimap, scoreboard, text chat, voice chat, inventory screen, or
per-player statistic tracking in the MVP.

## 7. Technical architecture

### 7.1 Packages and project configuration

Create a Unity **6000.3 LTS** Universal 3D project and resolve package versions
that the editor marks compatible instead of hard-coding speculative versions.

Required packages:

- `com.unity.services.multiplayer` — Multiplayer Services SDK, Sessions, and Relay integration.
- `com.unity.netcode.gameobjects` — GameObject/MonoBehaviour networking.
- `com.unity.transport` — installed as required by the networking stack.
- `com.unity.inputsystem` — generated keyboard/mouse input actions.
- `com.unity.cinemachine` — third-person orbit and camera collision support.
- `com.unity.multiplayer.playmode` — local multi-instance testing.
- `com.unity.multiplayer.tools` — network statistics and impaired-network testing.
- Unity Test Framework — Edit Mode and Play Mode automated tests.

Link the project to a Unity Cloud project, enable Authentication and Multiplayer
Services, and deploy any required configuration through the Editor tooling. Use
anonymous Authentication for the MVP; display names are session-local and are
not accounts.

### 7.2 Topology and trust model

The room creator is an NGO host: both a client and the authoritative server. All
other players connect to that host through Relay. Relay avoids inbound port
forwarding and a dedicated gameplay server, but it does **not** make the host
neutral or cheat-proof.

The host owns authority over:

- roster, role claims, ready states, and scene changes;
- match phase, start/end network time, score, and win result;
- food state, pickup arbitration, drops, and deposits;
- swatter cooldown and hit validation;
- death/respawn state and spawn selection.

Movement uses owner-responsive kinematic simulation with an owner-authoritative
`NetworkTransform`. The host monitors maximum speed, playable bounds, and invalid
teleports, correcting clear violations. This is appropriate for a friendly-room
MVP and avoids round-trip input lag through Relay. It is not competitive-grade
anti-cheat or full client prediction/reconciliation.

Swats are requested by the Human owner but resolved by the host using the host's
observed player poses, a server cooldown, role/state checks, and a slightly
forgiving swept volume. Do not accept client-supplied hit lists. Full lag
compensation/rewind is deferred; tune the hit volume under simulated latency.

### 7.3 Scene flow

| Scene | Networked | Responsibility |
|---|---|---|
| `Bootstrap` | No | Persistent app root, services initialization, audio/settings, loading/error UI |
| `FrontEnd` | No | Create/join by room code |
| `Lobby` | Yes | Connected roster, seat selection, ready state, start gate |
| `Kitchen` | Yes | Match simulation, player spawning, HUD, results |

`NetworkManager` and the session coordinator live on a persistent app root.
Only the host requests network scene changes through NGO scene management. Clients
never load a gameplay scene independently.

### 7.4 Runtime systems

| System | Main responsibility | Authority |
|---|---|---|
| `ServicesBootstrap` | Initialize UGS and anonymous sign-in once | Local |
| `SessionCoordinator` | Create/join/leave Session; expose code and connection errors | Local + UGS |
| `NetworkRoster` | Player identity, seat, ready flag, disconnect cleanup | Host |
| `NetworkGameManager` | State machine, timer, score, victory, rematch | Host |
| `NetworkPlayerSpawner` | Spawn role prefab and assign client ownership | Host |
| `CockroachMotor` | Owner input, kinematic motion, carry speed modifier | Owner; host bounds checks |
| `HumanMotor` | Owner input and first-person motion | Owner; host bounds checks |
| `FoodItem` | Size/value and world/carried/deposited state | Host |
| `FoodInteraction` | Validate pickup, drop, and deposit requests | Host |
| `SwatterController` | Local presentation; request and validate swings/hits | Owner presentation; host result |
| `RespawnController` | Disable, delay, select nest point, restore player | Host |
| `RoleCameraController` | Enable only the owning player's camera/listener | Local owner |
| `MatchHUDPresenter` | Observe replicated state without owning game rules | Local |

### 7.5 Networked state

Use small replicated state and event RPCs, not synchronized MonoBehaviour logic.

- `NetworkGameManager`: phase, match end time/tick, deposited points, winner.
- `NetworkRoster`: a `NetworkList` of client ID, short display name, role/seat,
  ready, and connected state.
- Player object: role, active/respawning state, carried food network ID, respawn end time.
- Food object: size, lifecycle (`World`, `Carried`, `Deposited`), carrier client ID.
- Transform snapshots: position and necessary rotation axes only, interpolated for
  non-owners and sent on an unreliable sequenced channel through `NetworkTransform`.

Use server RPCs for intentions such as `RequestSeat`, `SetReady`, `RequestPickup`,
`RequestDrop`, and `RequestSwat`. Validate caller ownership, match phase, distance,
cooldown, and current state in every handler. Use client RPCs only for transient
presentation that cannot be derived from replicated state, such as a confirmed
impact burst or one-shot result sting.

Do not send input or state every render frame over reliable RPCs. Keep network
ticks initially at 30 Hz and profile before changing that value.

### 7.6 Food representation

Each world food item is a spawned `NetworkObject`. The host resolves competing
pickup requests; the first valid request wins. While carried, disable the world
collider/rendering and show a non-networked visual of the matching size at the
carrier's socket on every client. This avoids fragile runtime parenting and an
extra transform stream. On drop, the host places the world item at a validated
floor position and restores it. On deposit, it despawns the item and increments
the score atomically.

### 7.7 Match state machine

```text
FrontEnd -> Lobby -> Loading -> Countdown -> Playing -> Results -> Lobby
                    |                         |
                    +---- connection loss ---+-> FrontEnd
```

Only `Playing` accepts movement-dependent interactions or decrements the timer.
The host stores an end timestamp derived from NGO network time; clients calculate
their display from that shared value rather than replicating a value every frame.
Score completion and timeout are resolved once by the host, with score completion
taking precedence if both occur in the same server tick.

## 8. Suggested asset and code layout

```text
Assets/CockroachFantasia/
  Art/{Characters,Environment,Food,VFX,UI}
  Audio/{Music,SFX}
  Data/{MatchRules,FoodDefinitions}
  Input/GameInputActions.inputactions
  Prefabs/{Characters,Food,Networking,Props,UI}
  Scenes/{Bootstrap,FrontEnd,Lobby,Kitchen}
  Scripts/
    Runtime/{App,Camera,Characters,Food,Gameplay,Networking,UI}
    Tests/{EditMode,PlayMode}
  Settings/{RenderPipeline,Volumes}
```

Use assembly definitions for `Runtime`, `Editor` if needed, `EditModeTests`, and
`PlayModeTests`. Keep gameplay rules in plain C# where practical so timing,
scoring, carrying modifiers, and transitions are testable without a live service.

## 9. Implementation sequence and gates

### Milestone 0 — Project foundation

- Create the Unity 6.3 LTS URP project and Windows x86-64 target.
- Add packages, assembly definitions, input actions, render settings, and scenes.
- Link the Unity Cloud project and confirm anonymous Authentication in Editor and build.
- Add a bootstrap state machine and user-readable async error handling.

**Gate:** A Windows development build reaches FrontEnd, authenticates, and can
recover cleanly from unavailable services.

### Milestone 1 — Network risk spike

- Configure NGO, Unity Transport, Multiplayer Services Sessions, and Relay DTLS.
- Create/join a private four-player room by Session code.
- Prove four local instances and two Windows machines can connect over Relay.
- Implement leave, host-loss handling, full-room rejection, and logging.

**Gate:** Four colored capsules remain connected for ten minutes through Relay;
host closure returns clients to FrontEnd with a useful message.

### Milestone 2 — Authoritative lobby

- Build roster replication, seat claiming, ready toggles, and host-only start.
- Validate exactly one Human and three Cockroaches.
- Lock late joins during loading/playing and synchronize NGO scene changes.

**Gate:** Repeated simultaneous seat claims never create an invalid roster, and
no client can start or enter a match before all four are ready.

### Milestone 3 — Greybox roles and map

- Greybox the three-lane kitchen, nest, bounds, and spawn points.
- Implement both motors and their local-only cameras/audio listeners.
- Replicate motion, add interpolation, enforce speed/bounds sanity checks, and
  apply carry speed modifiers.

**Gate:** Four players can traverse for ten minutes with no duplicate cameras,
ownership errors, out-of-bounds traps, or visually severe remote jitter at the
target latency profile.

### Milestone 4 — Food objective and match flow

- Implement food definitions, fixed spawns, pickup arbitration, carried visuals,
  validated drops, automatic nest deposits, score, timer, win conditions, and HUD.
- Implement countdown, results, and synchronized rematch back to Lobby.

**Gate:** Three Cockroaches can win by depositing 12 points; the Human can win by
timeout; all four clients always show the same score, remaining time, and result.

### Milestone 5 — Swatter and respawn

- Add swing timing, owner presentation, server cooldown and hit query.
- Add item drop, reaction state, safe delayed respawn, and spawn occupancy checks.
- Test edge cases: deposit versus hit, hit at timeout, disconnect while carrying,
  and multiple targets in one swing.

**Gate:** A Cockroach can be hit, drop food, and rejoin play indefinitely without
gore, permanent elimination, duplicated food, stuck input, or divergent state.

### Milestone 6 — Cute presentation and usability

- Replace essential greybox characters/food with simple stylized assets.
- Add role-readable animation, impact VFX, audio, UI transitions, prompts, and
  basic settings/accessibility options.
- Make interactable silhouettes and the Human's range readable without clutter.

**Gate:** A new test group can create a room, choose roles, finish a match, and
start a rematch without developer guidance.

### Milestone 7 — Stabilization and Windows release candidate

- Profile CPU, GPU, allocations, and bandwidth in four-player sessions.
- Exercise latency/loss, repeated scene cycles, disconnects, invalid codes, and
  service outages.
- Produce a Windows x86-64 development build, then a clean release candidate with
  versioned configuration and credits/licenses.

**Gate:** The release checklist in section 11 passes on two physical networks and
at least the minimum and recommended test PCs.

## 10. Test strategy

### Automated Edit Mode tests

- Match state transition table and start eligibility.
- Exactly-one-Human roster invariant.
- Food values and carry-speed calculation/clamping.
- Atomic deposit and score win precedence over same-tick timeout.
- Swat cooldown, target eligibility, and respawn scheduling.
- Disconnect cleanup, including a carried item returning safely to the world.
- Join-code normalization and user-facing error mapping.

### Automated Play Mode/network tests

- Host plus three clients spawn exactly one owned avatar each.
- Only owners read input or enable cameras/audio listeners.
- Two clients racing for one food item produce one carrier.
- Unauthorized role, pickup, deposit, and swat requests are rejected.
- Scene transition, late-join rejection, results, and rematch remain synchronized.

Tests must not require production UGS for ordinary CI. Wrap session operations
behind an interface and use fakes for unit tests; keep a smaller opt-in integration
suite for real Authentication/Session/Relay smoke tests.

### Manual network matrix

- Four Multiplayer Play Mode instances on one development PC.
- One host plus three standalone clients on LAN.
- Clients on at least two external networks through Relay.
- Baseline, 80 ms RTT, 150 ms RTT, 2% packet loss, and brief connection
  interruption using Multiplayer Tools where supported.
- Host as Human and host as Cockroach.
- 16:9 and 16:10 at 1080p, plus windowed/fullscreen and common mouse sensitivities.

The initial feel target is stable play at **150 ms RTT and 2% simulated loss**,
with no game-state divergence. That is a test target, not a guaranteed service SLA.

## 11. MVP release acceptance checklist

- Exactly four friends can create/join a private room with a shareable code.
- The lobby enforces one Human, three Cockroaches, and all-ready before start.
- Both roles have responsive, role-appropriate cameras and keyboard/mouse control.
- Three food sizes visibly apply their configured speed penalties.
- Pickup, drop, swat, respawn, deposit, timer, score, results, and rematch agree on
  every connected client.
- The match always ends at quota or four minutes and cannot award both teams.
- No action produces gore or permanently eliminates a player.
- Invalid/full room, voluntary leave, client disconnect, and host loss have clear outcomes.
- A complete match/rematch loop runs three consecutive times without errors,
  duplicate network objects, leaked audio listeners, or stale roster entries.
- Windows release build connects over Relay without Editor involvement.

## 12. Explicitly outside MVP

- Advanced role abilities, gadgets, traps, power-ups, classes, and ultimates.
- Public matchmaking, room browser, quick play, ranked play, or backfill.
- Progression, unlocks, achievements, cosmetics economy, accounts, or persistence.
- Additional maps, procedural layouts, or randomized food placement.
- Bots, tutorials, spectator mode, split-screen, controllers, or non-Windows builds.
- Voice/text chat, parties, friend lists, invitations, moderation, and reporting.
- Host migration, reconnect/resume, dedicated servers, competitive anti-cheat, and
  full rollback/lag-compensated movement.

## 13. Primary risks and mitigations

| Risk | MVP mitigation |
|---|---|
| Listen-server host has authority/latency advantage | Position the game as private casual play; validate critical rules; state host-loss behavior clearly |
| Relay latency makes swats feel unfair | Owner-responsive motion, visible wind-up, generous swept hit volume, latency simulation early |
| Scale difference causes camera/collision problems | Kinematic motors, primitive collision, camera collision, strict decoration collision rules |
| Food races duplicate or disappear items | One host-owned lifecycle and atomic server validation; targeted race/disconnect tests |
| Human can camp one route | Three sight-line-separated lanes, distributed food, protected nest interior, playtest heatmaps/notes |
| Four-player testing arrives too late | Prove four-player Session/Relay connectivity before building final mechanics |
| UGS API/package changes | Use the unified Multiplayer Services package, lock the verified manifest/lock file, isolate service code behind `SessionCoordinator` |

## 14. Rough effort assumption

For one experienced Unity multiplayer engineer using placeholder/simple art, this
plan is approximately **25–35 focused working days** to a tested MVP candidate:
roughly one week for foundation/network/lobby, two weeks for roles and the full
gameplay loop, one week for presentation, and one to two weeks for integration,
balance, and stabilization. Original production-quality character art, animation,
music, extensive device QA, and store release work would add separate time.

## 15. Official technical references

- [Unity Multiplayer Services SDK overview](https://docs.unity.com/relay/clients)
- [Build a Session with Netcode for GameObjects](https://docs.unity.com/mps-sdk/build-your-first-session)
- [Unity Relay overview](https://docs.unity.com/relay)
- [Relay DTLS configuration](https://docs.unity.com/relay/enable-dtls-encryption)
- [Multiplayer Services SDK FAQ](https://docs.unity.com/mps-sdk/faq)

The unified Multiplayer Services SDK should be used instead of the deprecated
standalone Lobby and Relay packages for a new Unity 6 project.
