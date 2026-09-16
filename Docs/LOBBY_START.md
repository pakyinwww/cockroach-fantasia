# Ready gate and match loading

Each client can change only its own ready flag because `NetworkRoster` derives
the player ID from NGO RPC receive metadata. A seat change clears that flag.
The host is the only client whose start request is accepted, and only while the
authoritative roster contains exactly one Human, three Cockroaches, and four
ready players.

Starting first locks both the replicated roster and the Unity Session. Kitchen
is then loaded once through NGO scene management; the persistent roster carries
the assigned seats into the match. Every lobby interaction is rejected while
loading, and all clients display the same full-screen loading panel.

If a player disconnects before the NGO scene request begins, the pending start
is cancelled and the room is reopened. Once NGO has accepted the scene request,
its scene event drives all remaining connected peers together, so an individual
client cannot enter a locally loaded match.
