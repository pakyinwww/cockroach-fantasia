# Match HUD

The Kitchen owns one screen-space HUD presenter. It observes `NetworkGameManager` and the local replicated player
components; it never changes match, food, attack, or respawn state.

Both roles see the authoritative remaining time and `FOOD current / 12`. The last ten seconds turn red and pulse.
Confirmed deposits briefly show `FOOD SECURED`, while confirmed swatter impacts show a compact `WHOMP` count.

Cockroaches see carried size, point value, exact speed percentage, the context-sensitive `E` prompt, and a server-time
respawn countdown. The Human sees a centre reticle and either `SWATTER READY` or a compact seconds-to-ready label—no
ability bar. Role panels rebind from the current local player after Kitchen load and hide safely while no player object
exists, so scene transitions and disconnect cleanup cannot leave stale gameplay UI visible.
