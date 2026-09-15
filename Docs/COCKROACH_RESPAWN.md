# Cartoon knockout and respawn

A confirmed server swat asks each hit Cockroach's `CockroachRespawn` component to begin a knockout. An already
respawning player or a request outside Playing is ignored. Any carried item is first released through the normal
server drop transaction, so it remains in the world exactly once.

The server replicates `IsRespawning` and an absolute server-time deadline three seconds in the future. While knocked
out, input, food interaction, and the CharacterController collider are disabled on every peer. The placeholder body
squashes flat and a round cartoon puff appears; there is no gore and the player object is never eliminated.

At the deadline the server prefers that player's assigned nest marker, falling back through the other Cockroach nest
markers when one is occupied. Simultaneous completions see players already restored earlier in the same server frame,
so they select distinct free positions. The motor is moved safely, normal body scale and collision return, and control
is restored if the match is still Playing.
