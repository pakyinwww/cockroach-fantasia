# Cockroach controller

The Cockroach uses a small kinematic `CharacterController`; it has no jump,
sprint, climb, stamina, or role ability. WASD is projected through the local
camera's horizontal axes, accelerated toward a configurable 3.2 m/s base speed,
and smoothly turns the body toward travel.

The owner-only close camera orbits from mouse delta with clamped pitch. Runtime
hooks expose sensitivity and inverted pitch for the settings UI. A filtered
sphere cast pulls the camera ahead of walls and props before they can obscure the
Cockroach. The prefab's Camera and AudioListener begin disabled and are enabled
only by `OnNetworkSpawn` for the owning client; non-owners return before reading
keyboard or mouse state.

`SetControlState` is the single match/respawn gate. It clears velocity and blocks
input whenever the match is not Playing or the Cockroach is respawning. The
prefab also includes a carry socket and implements kitchen recovery teleporting
through the kinematic controller.
