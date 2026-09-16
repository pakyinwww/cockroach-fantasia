# Food pickup and carrying

Cockroaches press `E` to interact with food. When empty-handed, the owner selects the nearest world food within
0.75 metres (with network object ID as the deterministic tie-breaker) and requests that exact object. When carrying,
`E` requests a drop.

The server accepts a pickup only while the match is Playing, from the owning Cockroach role, within range, when the
food is still in the World state and the Cockroach has no carried item. Claiming changes the food lifecycle before the
next request is evaluated, so concurrent requests have exactly one winner. Human prefabs do not contain the carrier
component.

Carried world renderers and colliders are disabled on every peer. Each peer reconstructs a non-networked display mesh
at that Cockroach's carry socket from the authoritative food definition, and the owning movement controller applies
the definition's speed multiplier. The replicated food ID and carrier client ID are the source of truth.

Drops are placed by a server-side floor raycast in front of the Cockroach. The server restores the item's World state,
clears both sides of the carrier relationship, and returns movement speed to normal. A server disconnect callback and
network-despawn fallback use the same release path, preventing carried food from being duplicated or stranded.
