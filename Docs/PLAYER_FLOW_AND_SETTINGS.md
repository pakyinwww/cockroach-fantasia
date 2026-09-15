# Player flow and local settings

The startup scene initializes Unity Services and automatically opens `FrontEnd` when anonymous sign-in succeeds.
Failures remain on the startup screen with readable status copy and a Retry button.

`FrontEnd` supports creating and joining private rooms, normalized room-code input, code copying, a session-local
display name, entry into the host-controlled role lobby, leaving, settings, and quitting. All actions provide visible
text status and are laid out by a 1920×1080 scale-with-screen-size canvas for 16:9 and 16:10 displays.

Escape opens the Kitchen overlay without changing `Time.timeScale`. Local movement, pickup, and swatter input are
suppressed while its cursor is active, but the authoritative online match continues. Leaving requires confirmation.

Mouse sensitivity, invert Y, master volume, fullscreen-window mode, and resolution are stored in `PlayerPrefs`.
Supported MVP resolutions are 1280×800, 1600×900, and 1920×1080; look settings apply to both roles.
