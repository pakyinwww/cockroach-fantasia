# Human swatter attack

The Human presses the left mouse button for one readable attack. The owning client immediately animates the comical
swatter through a 0.25-second wind-up and sends a target-free request. The server accepts requests only from the
Human player during Playing and enforces a 1.1-second server-time cooldown.

After the wind-up, the server uses its observed swatter socket pose to evaluate a 1.8-metre capsule-shaped sweep with
a forgiving 0.55-metre radius. It derives Cockroach targets from physics colliders, deduplicates them by network object
ID, and can confirm multiple hits in one swing. Clients never submit a target list or hit position.

Only a compact confirmed-impact RPC is replicated. Each peer creates a short impact puff at the averaged contact
position and records the confirmed hit count; the swing animation itself is not synchronized every frame. This keeps
the attack legible at the MVP latency target without adding rewind or lag compensation.
