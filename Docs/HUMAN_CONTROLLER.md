# Human controller

The Human uses a 1.8 metre kinematic capsule with accelerated WASD movement,
gravity, and collision sliding. There is no jump, sprint, gadget, or movement
ability. Mouse X turns the capsule and mouse Y pitches the first-person view
between -80 and +80 degrees; sensitivity and inverted pitch have runtime hooks.

The view sits inside the collision capsule with a 0.04 metre near plane, keeping
it stable against major geometry. The Kitchen's narrow, low nest entrance is
smaller than the Human capsule, so ordinary movement cannot enter it and the
CharacterController slides cleanly away instead of wedging. Outer world bounds
use the same physical collision path.

The Human network prefab includes an owner camera, AudioListener, and view-space
swatter socket. Camera and listener begin disabled, enable only for the owning
network client, and non-owners return before reading keyboard or mouse state.
`SetControlState` gates input outside active play and during interruption states.
