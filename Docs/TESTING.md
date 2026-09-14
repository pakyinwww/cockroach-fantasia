# Testing

The ordinary automated suite is deterministic and does not contact production
Unity Gaming Services. Runtime integrations are wrapped behind interfaces such as
`IUnityServicesGateway`; tests use local fakes.

## Editor

Open **Window > General > Test Runner** and run Edit Mode and Play Mode suites
independently.

## Batch mode on Windows

From the repository root, run the checked-in wrapper with the installed 6000.3
LTS editor path:

```powershell
.\Tools\Run-UnityTests.ps1 -EditorPath 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe' -Suite EditMode
.\Tools\Run-UnityTests.ps1 -EditorPath 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe' -Suite PlayMode
```

The wrapper propagates Unity's non-zero failure code and points to the actionable
XML result and full Editor log under `Builds`. CI should preserve both as artifacts.

`IPrivateSessionGateway` isolates the coordinator from the production Sessions
SDK. Edit Mode tests use `FakePrivateSessionGateway` and
`FakeUnityServicesGateway`. Play Mode includes a shared `NetworkTestFixture` that
connects a real NGO host and client over loopback, plus a Bootstrap scene smoke
test. None of these ordinary suites signs in to UGS.

## Live-service smoke tests

Real Authentication, Session, and Relay tests are opt-in because they consume
external services and require a linked Unity Cloud project. Run them manually in
the `production` environment before a release candidate. Never place access
tokens or service-account credentials in the repository.

## Determinism

Gameplay systems that depend on time or random selection receive `IGameClock`
and `IRandomSource` abstractions. Tests should supply fixed or manually advanced
implementations instead of waiting on wall-clock time or Unity randomness.
