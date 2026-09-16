# Results and rematch

The server publishes one authoritative winner and final deposited-food total through `NetworkGameManager`.
Every peer derives its role-specific result copy from that shared state. Clients see a waiting message; only the
host receives active Rematch and Return to Menu controls.

Rematch keeps the private Session and assigned roles, clears every ready flag, unlocks the room, and uses NGO
scene management to return all peers to `Lobby`. Loading `Kitchen` again creates fresh match, food, player, input,
camera, and HUD objects. Return to Menu synchronously loads `FrontEnd`, then each peer leaves the Session and
shuts down NGO.

Use `Tools/Run-MovementRelayDiagnostic.ps1 -ResultsRematch -SkipMovement` against a development build to run
three complete four-peer result/rematch cycles and a clean synchronized return to FrontEnd.
