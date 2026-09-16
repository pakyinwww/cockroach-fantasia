# Session lifecycle behavior

`SessionCoordinator` owns one Session handle and one NGO connection at a time.
It maps service errors into stable player-facing categories, validates room codes
before contacting Unity Services, and always tears down Session callbacks and NGO
state before a new connection attempt.

| Failure | Player-facing outcome |
|---|---|
| Invalid code | Input remains usable with a correction message |
| Expired or missing room | Return/remain on FrontEnd with an expiry message |
| Full room | Remain on FrontEnd and show `4/4` |
| Locked match | Remain on FrontEnd and explain that play has started |
| Unreachable service/Relay | Remain on FrontEnd with retry guidance |
| Listen-server host loss | Stop NGO, discard the Session handle, load FrontEnd, and show `Host left` |

The Session maximum rejects a fifth member. Before loading a match, the host calls
`SetSessionLockedAsync(true)`; rematch lobby flow reopens it with `false`. No host
migration or reconnect/resume path exists in the MVP.

`SessionLifecycleCommandLineRunner` supplies opt-in Windows checks for expected
join rejection, leave/rejoin cleanup, locking, and host loss. These use production
Services only when their explicit command-line switches are present.
