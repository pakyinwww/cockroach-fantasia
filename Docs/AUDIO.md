# MVP audio

`GameAudio` synthesizes the MVP's tiny footsteps, heavy footsteps, food rustles, pickup/drop chirps,
rubbery swat, harmless impact squeak, deposit/score flourish, countdown tick, and two role-specific
result stings. No third-party recordings are shipped.

All pooled voices route through `Resources/Audio/CockroachMixer.mixer`. The persisted Master Volume
setting applies immediately through `AudioListener.volume` and remains active across scene changes.
Short per-cue cooldowns prevent rapid replicated events or multi-target swats from producing an
uncomfortable pile-up of identical one-shots.
