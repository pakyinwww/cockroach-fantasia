# Unity Services setup

Cockroach Fantasia uses the unified Multiplayer Services package. Do not add the
deprecated standalone Lobby or Relay packages.

The repository is linked to the **Cockroach Fantasia** Unity Cloud project in the
`pakyinwww` organization. Its public Cloud Project ID is serialized in
`ProjectSettings/ProjectSettings.asset`; access tokens and local account
credentials must never be committed.

Authentication uses anonymous sign-in only. Sessions and Relay will provide the
private lobby and peer-hosted gameplay connection; no Matchmaker queue is needed
for the MVP.

## Runtime behavior

`ServicesBootstrap` is created before the first scene and survives scene loads.
It initializes Unity Services once, signs the player in anonymously, and exposes
state and user-readable status through `StatusChanged`. Concurrent callers share
one initialization task. Failed or cancelled attempts may be retried without
restarting the game. The default request timeout is 15 seconds.

The front end can bind a `ServicesStatusPresenter` to a status label and Retry
button. Automated test runs skip automatic service initialization.

## Verification

- Editor: execute `CockroachFantasia.Editor.UnityServicesSmokeTest.Run`. It
  enters Play Mode, authenticates, and exits with code 0.
- Windows development player: launch the build with `-servicesSmokeTest`. The
  player exits with code 0 after anonymous authentication and writes
  `UNITY_SERVICES_PLAYER_SMOKE_SUCCESS` to its log.
- Offline behavior: disconnect networking before launch and confirm the state
  becomes `Failed` with a retry message, then reconnect and retry without a
  restart.

Neither automated smoke check logs access tokens or other credentials.
